import type { ReactNode } from "react";
import { ApiError } from "../api/client";
export function Feedback({
  title,
  children,
  retry,
  error = false,
}: {
  title: string;
  children?: ReactNode;
  retry?: () => void;
  error?: boolean;
}) {
  return (
    <div
      className={`feedback ${error ? "failure" : ""}`}
      role={error ? "alert" : "status"}
    >
      <h2>{title}</h2>
      {children && <p>{children}</p>}
      {retry && (
        <button className="button secondary" onClick={retry}>
          Try again
        </button>
      )}
    </div>
  );
}
export function Loading({ label = "Loading…" }: { label?: string }) {
  return (
    <div className="feedback" role="status">
      <span className="spinner" aria-hidden="true" />
      <p>{label}</p>
    </div>
  );
}
export function ErrorState({
  error,
  retry,
}: {
  error: ApiError;
  retry?: () => void;
}) {
  return (
    <Feedback
      error
      title={
        error.status === 403
          ? "Access denied"
          : error.status === 404
            ? "Not found"
            : "Unable to load"
      }
      retry={retry}
    >
      {error.message}
    </Feedback>
  );
}
