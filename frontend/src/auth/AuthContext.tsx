import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { ApiError, onUnauthorized, request } from "../api/client";
import type { LoginResult, UserInfo } from "../api/types";
import {
  clearToken,
  expiresAt,
  readToken,
  TOKEN_KEY,
  validToken,
} from "./session";

type Status = "loading" | "anonymous" | "authenticated" | "error" | "forbidden";
interface Auth {
  status: Status;
  user: UserInfo | null;
  error: string;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  restore: () => Promise<void>;
}
const Context = createContext<Auth | null>(null);
export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<Status>("loading");
  const [user, setUser] = useState<UserInfo | null>(null);
  const [error, setError] = useState("");
  const generation = useRef(0);
  const logout = useCallback(() => {
    generation.current++;
    clearToken();
    setUser(null);
    setError("");
    setStatus("anonymous");
  }, []);
  const restore = useCallback(async () => {
    const current = ++generation.current;
    const token = readToken();
    setUser(null);
    setError("");
    if (!validToken(token)) {
      clearToken();
      setStatus("anonymous");
      return;
    }
    setStatus("loading");
    try {
      const result = await request<UserInfo>("/User/me");
      if (current !== generation.current) return;
      setUser(result);
      setStatus(
        result.roles.some((r) => r === "Admin" || r === "Agent")
          ? "authenticated"
          : "forbidden",
      );
    } catch (e) {
      if (current !== generation.current) return;
      const failure = e as ApiError;
      setError(failure.message);
      if (failure.status === 401) logout();
      else setStatus(failure.status === 403 ? "forbidden" : "error");
    }
  }, [logout]);
  useEffect(() => {
    const unsubscribe = onUnauthorized(logout);
    void restore();
    return () => {
      unsubscribe();
      generation.current++;
    };
  }, [logout, restore]);
  useEffect(() => {
    if (status === "anonymous" || status === "loading") return;
    const check = () => {
      if (!validToken(readToken())) logout();
    };
    const timer = window.setTimeout(
      check,
      Math.min(
        Math.max(expiresAt(readToken() || "") - Date.now(), 0),
        2147483647,
      ),
    );
    window.addEventListener("focus", check);
    return () => {
      clearTimeout(timer);
      window.removeEventListener("focus", check);
    };
  }, [status, logout]);
  const login = async (email: string, password: string) => {
    const current = ++generation.current;
    const result = await request<LoginResult>("/Auth/login", {
      method: "POST",
      data: { email: email.trim(), password },
      public: true,
    });
    if (current !== generation.current) return;
    if (!validToken(result.token))
      throw new ApiError(
        401,
        "The API returned an invalid or expired session.",
      );
    sessionStorage.setItem(TOKEN_KEY, result.token);
    await restore();
  };
  return (
    <Context.Provider value={{ status, user, error, login, logout, restore }}>
      {children}
    </Context.Provider>
  );
}
export function useAuth() {
  const value = useContext(Context);
  if (!value) throw new Error("AuthProvider is required");
  return value;
}
