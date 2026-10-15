import { useState, type FormEvent } from "react";
import { Navigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Loading } from "../components/Feedback";
export function Login() {
  const auth = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  if (auth.status === "loading")
    return <Loading label="Checking your session…" />;
  if (auth.status === "authenticated")
    return <Navigate to="/listings" replace />;
  if (auth.status === "forbidden") return <Navigate to="/forbidden" replace />;
  if (auth.status === "error") return <Navigate to="/listings" replace />;
  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    if (!email.trim() || !password) {
      setError("Enter your email and password.");
      return;
    }
    setBusy(true);
    try {
      await auth.login(email, password);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="login-shell">
      <section className="login-story">
        <div className="brand">
          REMP<span>PROPERTY MEDIA</span>
        </div>
        <div>
          <p className="eyebrow">YOUR PROPERTY, PRESENTED.</p>
          <h1>
            A clear view.
            <br />
            Every detail.
          </h1>
          <p>One place to access your property listings and delivered media.</p>
        </div>
        <p className="fine-print">Property media delivery</p>
      </section>
      <section className="login-panel">
        <form onSubmit={submit} className="login-form">
          <p className="eyebrow">WELCOME BACK</p>
          <h2>Sign in to REMP</h2>
          <p className="muted">
            Use your account email to access your listings.
          </p>
          <label htmlFor="email">Email address</label>
          <input
            id="email"
            type="email"
            autoComplete="username"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            disabled={busy}
          />
          <label htmlFor="password">Password</label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={busy}
          />
          {error && (
            <p className="inline-error" role="alert">
              {error}
            </p>
          )}
          <button className="button primary" disabled={busy}>
            {busy ? "Signing in…" : "Sign in"}
          </button>
          <p className="fine-print">
            Need access? Contact your photography company.
          </p>
        </form>
      </section>
    </main>
  );
}
