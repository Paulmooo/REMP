import { Link, Navigate, Route, Routes, useLocation } from "react-router-dom";
import { useEffect } from "react";
import { useAuth } from "./auth/AuthContext";
import { Layout } from "./components/Layout";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { Feedback, Loading } from "./components/Feedback";
import { Login } from "./pages/Login";
import { Listings } from "./pages/Listings";
import { ListingDetail } from "./pages/ListingDetail";
function Forbidden() {
  const auth = useAuth();
  if (auth.status === "loading") return <Loading label="Checking your session…" />;
  if (auth.status === "anonymous") return <Navigate to="/login" replace />;
  return (
    <main className="session-error">
      <Feedback error title="Access denied">
        {auth.error || "Your account does not have access to this workspace."}
      </Feedback>
      <button className="button secondary" onClick={auth.logout}>
        Sign out
      </button>
    </main>
  );
}
export function App() {
  const { pathname } = useLocation();
  useEffect(() => {
    window.scrollTo(0, 0);
    document.title = `${pathname === "/login" ? "Sign in" : pathname === "/listings" ? "Listings" : "Property details"} · REMP`;
  }, [pathname]);
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/forbidden" element={<Forbidden />} />
      <Route element={<ProtectedRoute />}>
        <Route element={<Layout />}>
          <Route index element={<Navigate to="/listings" replace />} />
          <Route path="/listings" element={<Listings />} />
          <Route path="/listings/:id" element={<ListingDetail />} />
          <Route
            path="*"
            element={
              <Feedback error title="Page not found">
                <Link to="/listings">Back to listings</Link>
              </Feedback>
            }
          />
        </Route>
      </Route>
    </Routes>
  );
}
