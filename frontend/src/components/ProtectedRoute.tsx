import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Feedback, Loading } from "./Feedback";
export function ProtectedRoute() {
  const auth = useAuth();
  if (auth.status === "loading")
    return <Loading label="Checking your session…" />;
  if (auth.status === "anonymous") return <Navigate to="/login" replace />;
  if (auth.status === "forbidden") return <Navigate to="/forbidden" replace />;
  if (auth.status === "error")
    return (
      <main className="session-error">
        <Feedback
          error
          title="Unable to verify your session"
          retry={() => void auth.restore()}
        >
          {auth.error}
        </Feedback>
        <button className="button secondary" onClick={auth.logout}>
          Back to sign in
        </button>
      </main>
    );
  return <Outlet />;
}
