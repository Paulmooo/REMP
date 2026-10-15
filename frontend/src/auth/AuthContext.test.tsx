import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, request } from "../api/client";
import { AuthProvider, useAuth } from "./AuthContext";
import { TOKEN_KEY, expiresAt, validToken } from "./session";
import { ProtectedRoute } from "../components/ProtectedRoute";
vi.mock("../api/client", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../api/client")>()),
  request: vi.fn(),
}));
const call = vi.mocked(request);
function token(exp = Date.now() / 1000 + 3600) {
  return `header.${btoa(JSON.stringify({ exp, roles: ["Admin"] }))}.signature`;
}
function Probe() {
  const auth = useAuth();
  return (
    <>
      <p>{auth.status}</p>
      <p>{auth.user?.roles.join(",")}</p>
      <button onClick={auth.logout}>Logout</button>
      <button onClick={() => void auth.restore()}>Retry</button>
    </>
  );
}
function mount() {
  return render(
    <AuthProvider>
      <Probe />
    </AuthProvider>,
  );
}
afterEach(() => {
  cleanup();
  sessionStorage.clear();
  vi.resetAllMocks();
  vi.useRealTimers();
});
describe("identity restoration", () => {
  it("does not call me without a token or with an expired/malformed token", async () => {
    sessionStorage.setItem(TOKEN_KEY, token(Date.now() / 1000 - 10));
    mount();
    await screen.findByText("anonymous");
    expect(call).not.toHaveBeenCalled();
    expect(sessionStorage.getItem(TOKEN_KEY)).toBeNull();
    expect(expiresAt("broken")).toBe(0);
    expect(validToken(null)).toBe(false);
  });
  it("restores using me roles rather than decoded JWT roles", async () => {
    sessionStorage.setItem(TOKEN_KEY, token());
    call.mockResolvedValue({ id: "a", roles: ["Agent"], listingCaseIds: [] });
    mount();
    await screen.findByText("authenticated");
    expect(screen.getByText("Agent")).toBeTruthy();
    expect(call).toHaveBeenCalledWith("/User/me");
  });
  it("keeps a 403 session while refusing protected content", async () => {
    const value = token();
    sessionStorage.setItem(TOKEN_KEY, value);
    call.mockRejectedValue(new ApiError(403, "Denied"));
    mount();
    await screen.findByText("forbidden");
    expect(sessionStorage.getItem(TOKEN_KEY)).toBe(value);
  });
  it("clears an invalid session on me 401", async () => {
    sessionStorage.setItem(TOKEN_KEY, token());
    call.mockRejectedValue(new ApiError(401, "Expired"));
    mount();
    await screen.findByText("anonymous");
    expect(sessionStorage.getItem(TOKEN_KEY)).toBeNull();
  });
  it("offers retry after connection failure without discarding a valid token", async () => {
    const value = token();
    sessionStorage.setItem(TOKEN_KEY, value);
    call
      .mockRejectedValueOnce(new ApiError(0, "Offline"))
      .mockResolvedValue({ id: "a", roles: ["Admin"], listingCaseIds: [] });
    mount();
    await screen.findByText("error");
    expect(sessionStorage.getItem(TOKEN_KEY)).toBe(value);
    fireEvent.click(screen.getByText("Retry"));
    await screen.findByText("authenticated");
  });
  it("does not restore a late me response after logout", async () => {
    sessionStorage.setItem(TOKEN_KEY, token());
    let resolve!: (value: unknown) => void;
    call.mockReturnValue(
      new Promise((r) => {
        resolve = r;
      }),
    );
    mount();
    fireEvent.click(screen.getByText("Logout"));
    await act(async () =>
      resolve({ id: "a", roles: ["Admin"], listingCaseIds: [] }),
    );
    expect(screen.getByText("anonymous")).toBeTruthy();
  });
  it("expires a session while the page remains open", async () => {
    sessionStorage.setItem(TOKEN_KEY, token(Date.now() / 1000 + 10));
    call.mockResolvedValue({ id: "a", roles: ["Agent"], listingCaseIds: [] });
    mount();
    await screen.findByText("authenticated");
    vi.useFakeTimers();
    vi.setSystemTime(Date.now() + 20000);
    fireEvent(window, new Event("focus"));
    expect(screen.getByText("anonymous")).toBeTruthy();
  });
});
describe("protected routes", () => {
  function router() {
    return render(
      <MemoryRouter initialEntries={["/listings/1"]}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<p>Sign in screen</p>} />
            <Route path="/forbidden" element={<p>No permission screen</p>} />
            <Route element={<ProtectedRoute />}>
              <Route path="/listings/:id" element={<p>Private listing</p>} />
            </Route>
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );
  }
  it("redirects direct anonymous access to login", async () => {
    router();
    await screen.findByText("Sign in screen");
    expect(screen.queryByText("Private listing")).toBeNull();
  });
  it("waits for me before revealing private data", async () => {
    sessionStorage.setItem(TOKEN_KEY, token());
    let resolve!: (value: unknown) => void;
    call.mockReturnValue(
      new Promise((r) => {
        resolve = r;
      }),
    );
    router();
    expect(screen.queryByText("Private listing")).toBeNull();
    await act(async () =>
      resolve({ id: "a", roles: ["Agent"], listingCaseIds: [] }),
    );
    await screen.findByText("Private listing");
  });
  it("denies unsupported server roles", async () => {
    sessionStorage.setItem(TOKEN_KEY, token());
    call.mockResolvedValue({ id: "u", roles: ["User"], listingCaseIds: [] });
    router();
    await screen.findByText("No permission screen");
  });
  it("keeps protected data hidden on network failure", async () => {
    sessionStorage.setItem(TOKEN_KEY, token());
    call.mockRejectedValue(new ApiError(0, "Offline"));
    router();
    await waitFor(() =>
      expect(screen.getByText("Unable to verify your session")).toBeTruthy(),
    );
    expect(screen.queryByText("Private listing")).toBeNull();
  });
});
