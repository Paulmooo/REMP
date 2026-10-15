import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  api,
  ApiError,
  normalizeError,
  onUnauthorized,
  request,
} from "./client";
import { TOKEN_KEY } from "../auth/session";
function token() {
  return `header.${btoa(JSON.stringify({ exp: Date.now() / 1000 + 3600 }))}.signature`;
}
function httpError(status: number, data?: unknown) {
  return new AxiosError("HTTP error", "", undefined, undefined, {
    status,
    data,
    statusText: "",
    headers: {},
    config: { headers: new AxiosHeaders() },
  });
}
afterEach(() => {
  sessionStorage.clear();
});
describe("API contract and session boundaries", () => {
  it("unwraps successful envelopes and sends the bearer token", async () => {
    const value = token();
    sessionStorage.setItem(TOKEN_KEY, value);
    const spy = vi
      .spyOn(api, "request")
      .mockResolvedValue({ data: { success: true, data: { id: 1 } } });
    expect(await request("/User/me")).toEqual({ id: 1 });
    expect(spy.mock.calls[0][0].headers).toEqual({
      Authorization: `Bearer ${value}`,
    });
  });
  it("clears only the active session on an empty-body 401", async () => {
    sessionStorage.setItem(TOKEN_KEY, token());
    const listener = vi.fn();
    const stop = onUnauthorized(listener);
    vi.spyOn(api, "request").mockRejectedValue(httpError(401, ""));
    await expect(request("/User/me")).rejects.toMatchObject({ status: 401 });
    expect(sessionStorage.getItem(TOKEN_KEY)).toBeNull();
    expect(listener).toHaveBeenCalledOnce();
    stop();
  });
  it("keeps the session on 403, including empty bodies", async () => {
    const value = token();
    sessionStorage.setItem(TOKEN_KEY, value);
    vi.spyOn(api, "request").mockRejectedValue(httpError(403));
    await expect(request("/ListingCase/listings/2")).rejects.toMatchObject({
      status: 403,
    });
    expect(sessionStorage.getItem(TOKEN_KEY)).toBe(value);
  });
  it("does not invalidate a new session when an older request returns 401", async () => {
    sessionStorage.setItem(TOKEN_KEY, token());
    vi.spyOn(api, "request").mockImplementation(async () => {
      sessionStorage.setItem(TOKEN_KEY, "new-session");
      throw httpError(401);
    });
    await expect(request("/User/me")).rejects.toBeInstanceOf(ApiError);
    expect(sessionStorage.getItem(TOKEN_KEY)).toBe("new-session");
  });
  it("does not attach authorization or expire the session on login failures", async () => {
    const spy = vi
      .spyOn(api, "request")
      .mockRejectedValue(
        httpError(401, { Message: "Invalid email or password." }),
      );
    await expect(request("/Auth/login", { public: true })).rejects.toThrow(
      "Invalid email or password.",
    );
    expect(spy.mock.calls[0][0].headers).toBeUndefined();
  });
  it("rejects expired tokens before sending requests", async () => {
    sessionStorage.setItem(TOKEN_KEY, "bad");
    const spy = vi.spyOn(api, "request");
    await expect(request("/User/me")).rejects.toMatchObject({ status: 401 });
    expect(spy).not.toHaveBeenCalled();
  });
  it("normalizes uppercase global errors, validation envelopes and network failures", () => {
    expect(
      normalizeError(
        httpError(403, {
          StatusCode: 403,
          Message: "Company denied",
          ErrorType: "Forbidden",
          Detail: "/api",
        }),
      ).message,
    ).toBe("Company denied");
    expect(
      normalizeError(
        httpError(400, { message: "Invalid", errors: ["Email required"] }),
      ).message,
    ).toBe("Invalid Email required");
    expect(
      normalizeError(
        httpError(400, {
          title: "Validation failed",
          errors: { Email: ["Bad email"] },
        }),
      ).message,
    ).toBe("Validation failed Bad email");
    expect(
      normalizeError(httpError(500, "<html>Bad gateway</html>")).message,
    ).toContain("unavailable");
    expect(normalizeError(new AxiosError("Network Error")).status).toBe(0);
  });
  it("rejects a failed or malformed success envelope", async () => {
    vi.spyOn(api, "request").mockResolvedValue({
      status: 200,
      data: { success: false, message: "Rejected" },
    });
    await expect(request("/Auth/login", { public: true })).rejects.toThrow(
      "Rejected",
    );
  });
});
