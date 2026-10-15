export const TOKEN_KEY = "remp.token";
// Decode only expiry, never trust client-decoded roles or identity for authorization.
export function expiresAt(token: string): number {
  try {
    const part = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
    const { exp } = JSON.parse(atob(part));
    return typeof exp === "number" && Number.isFinite(exp) ? exp * 1000 : 0;
  } catch {
    return 0;
  }
}
export function validToken(token: string | null): token is string {
  return !!token && expiresAt(token) > Date.now();
}
export function readToken() {
  return sessionStorage.getItem(TOKEN_KEY);
}
export function clearToken() {
  sessionStorage.removeItem(TOKEN_KEY);
}
