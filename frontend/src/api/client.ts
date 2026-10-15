import axios from "axios";
import { clearToken, readToken, validToken } from "../auth/session";

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}
export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || "/api",
  timeout: 15000,
});
const listeners = new Set<() => void>();
export function onUnauthorized(callback: () => void) {
  listeners.add(callback);
  return () => {
    listeners.delete(callback);
  };
}
function invalidate(token: string | null) {
  if (token && readToken() === token) {
    clearToken();
    listeners.forEach((fn) => fn());
  }
}
export function normalizeError(error: unknown, login = false): ApiError {
  if (error instanceof ApiError) return error;
  if (!axios.isAxiosError(error))
    return new ApiError(0, "Something went wrong. Please try again.");
  const status = error.response?.status || 0;
  const body = error.response?.data;
  const fallback =
    status === 401
      ? login
        ? "Unable to sign in. Check your email, password and account access."
        : "Your session has expired. Please sign in again."
      : status === 403
        ? "You do not have permission to view this content."
        : status === 404
          ? "This resource could not be found."
          : status >= 500
            ? "The service is unavailable. Please try again."
            : status === 0
              ? "Unable to connect to the API. Check the service and your connection."
              : "The request could not be completed.";
  const message =
    body && typeof body === "object"
      ? body.message || body.Message || body.title
      : null;
  const errors = body?.errors || body?.Errors;
  const details = Array.isArray(errors)
    ? errors.filter((x) => typeof x === "string")
    : errors && typeof errors === "object"
      ? Object.values(errors)
          .flat()
          .filter((x) => typeof x === "string")
      : [];
  return new ApiError(
    status,
    [typeof message === "string" ? message : fallback, ...details].join(" "),
  );
}
// All authenticated requests use the same expiry and HTTP error behavior.
export async function request<T>(
  url: string,
  options: {
    method?: "GET" | "POST" | "PUT";
    data?: unknown;
    signal?: AbortSignal;
    public?: boolean;
  } = {},
): Promise<T> {
  const token = options.public ? null : readToken();
  if (!options.public && !validToken(token)) {
    invalidate(token);
    throw new ApiError(401, "Your session has expired. Please sign in again.");
  }
  try {
    const response = await api.request({
      url,
      method: options.method || "GET",
      data: options.data,
      signal: options.signal,
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    });
    const body = response.data;
    if (body?.success !== true || !("data" in body))
      throw new ApiError(
        response.status,
        body?.message || "The API returned an unexpected response.",
      );
    return body.data as T;
  } catch (error) {
    if (axios.isCancel(error)) throw error;
    const normalized = normalizeError(error, options.public);
    if (normalized.status === 401 && !options.public) invalidate(token);
    throw normalized;
  }
}
