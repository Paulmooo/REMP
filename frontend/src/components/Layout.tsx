import { Link, NavLink, Outlet } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
export function Layout() {
  const { user, logout } = useAuth();
  const admin = user?.roles.includes("Admin");
  return (
    <>
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      <header className="header">
        <Link to="/listings" className="brand">
          REMP<span>PROPERTY MEDIA</span>
        </Link>
        <nav aria-label="Main navigation">
          <NavLink to="/listings">
            {admin ? "Company listings" : "Assigned listings"}
          </NavLink>
        </nav>
        <div className="account">
          <div>
            <span className="role">
              {user?.roles
                .filter((r) => r === "Admin" || r === "Agent")
                .join(" · ")}
            </span>
            <span className="user-id" title={user?.id}>
              User {user?.id}
            </span>
          </div>
          <button className="button secondary compact" onClick={logout}>
            Sign out
          </button>
        </div>
      </header>
      <main id="main" className="main">
        <Outlet />
      </main>
      <footer className="footer">REMP · Property media delivery</footer>
    </>
  );
}
