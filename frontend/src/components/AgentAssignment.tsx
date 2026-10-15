import { useEffect, useRef, useState } from "react";
import { normalizeError, request } from "../api/client";
import type { Agent, CompanyAgent } from "../api/types";
import { useResource } from "../api/useResource";
import { ErrorState, Feedback, Loading } from "./Feedback";

const name = (agent: Agent) => [agent.agentFirstName, agent.agentLastName].filter(Boolean).join(" ") || agent.id;

// Mounted only for Admin. Candidates are never requested by the Agent view.
export function AgentAssignment({ listingId, agents, refresh }: {
  listingId: string;
  agents: Agent[];
  refresh: (signal: AbortSignal) => Promise<void>;
}) {
  const [open, setOpen] = useState(false);
  const [visited, setVisited] = useState(false);
  return <div className="agent-management">
    <button className="button secondary" aria-expanded={open} aria-controls="agent-assignment" onClick={() => { setVisited(true); setOpen(!open); }}>Manage agents</button>
    {visited && <div hidden={!open}><Manager listingId={listingId} agents={agents} refresh={refresh} /></div>}
  </div>;
}
function Manager({ listingId, agents, refresh }: {
  listingId: string; agents: Agent[]; refresh: (signal: AbortSignal) => Promise<void>;
}) {
  const { data: candidates, error: loadError, retry } = useResource<CompanyAgent[]>("/User/agents");
  const [busy, setBusy] = useState(false);
  const [phase, setPhase] = useState<"idle" | "refresh" | "done">("idle");
  const [error, setError] = useState("");
  const [confirm, setConfirm] = useState<Agent | null>(null);
  const lock = useRef(false);
  const lifecycle = useRef<AbortController | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    lifecycle.current = controller;
    return () => controller.abort();
  }, []);
  async function readDetail(signal: AbortSignal) {
    try {
      await refresh(signal);
      if (!signal.aborted) { setPhase("done"); setError(""); }
    } catch (e) {
      if (!signal.aborted) setError(`Change saved, but listing details could not be refreshed. ${normalizeError(e).message}`);
    }
  }
  async function save(agent: Agent, assigned: boolean) {
    if (lock.current || phase === "refresh" || agents.some(a => a.id === agent.id) === assigned) return;
    const signal = lifecycle.current!.signal;
    lock.current = true; setBusy(true); setError(""); setPhase("idle");
    try {
      await request(`/ListingCase/listings/${listingId}/agents/${encodeURIComponent(agent.id)}`, { method: assigned ? "PUT" : "DELETE", signal });
      if (signal.aborted) return;
      setConfirm(null); setPhase("refresh");
      await readDetail(signal);
    } catch (e) {
      if (!signal.aborted) setError(`${normalizeError(e).message} Displayed assignments have not been changed. If the connection failed, refresh the listing before retrying; the server may have saved the change.`);
    } finally {
      lock.current = false;
      if (!signal.aborted) setBusy(false);
    }
  }
  async function retryRead() {
    if (lock.current) return;
    const signal = lifecycle.current!.signal;
    lock.current = true; setBusy(true);
    await readDetail(signal);
    lock.current = false; if (!signal.aborted) setBusy(false);
  }
  const disabled = busy || phase === "refresh";
  return <div id="agent-assignment" className="assignment-panel">
    <h3>Listing agent assignment</h3>
    <p className="muted">Choose agents already in your company. Assignment does not activate an account or send an invitation.</p>
    {!candidates && !loadError && <Loading label="Loading company agents…" />}
    {loadError && <ErrorState error={loadError} retry={retry} />}
    {candidates && (candidates.length ? <ul className="assignment-list">{candidates.map(agent => {
      const assigned = agents.some(a => a.id === agent.id);
      return <li key={agent.id}>
        <div><strong>{name(agent)}</strong>{agent.email && <p>{agent.email}</p>}{agent.companyName && <p className="muted">{agent.companyName}</p>}</div>
        <button className="button secondary" disabled={disabled || assigned || !!confirm} onClick={() => void save(agent, true)} aria-label={`${assigned ? "Assigned" : "Assign"} ${name(agent)}`}>{assigned ? "Assigned" : "Assign"}</button>
      </li>;
    })}</ul> : <Feedback title="No company agents">There are no agents available in your company.</Feedback>)}
    {agents.length > 0 && <><h3>Current assignments</h3><ul className="assignment-list">{agents.map(agent => <li key={agent.id}>
      <strong>{name(agent)}</strong><button className="button secondary" disabled={disabled || !!confirm} onClick={() => setConfirm(agent)} aria-label={`Unassign ${name(agent)}`}>Unassign</button>
    </li>)}</ul></>}
    {confirm && <div className="assignment-confirm" role="group" aria-label="Confirm unassignment">
      <p>Unassign {name(confirm)} from this listing? Their company membership will remain.</p>
      <div className="assignment-actions"><button className="button secondary" disabled={disabled} onClick={() => setConfirm(null)}>Cancel</button><button className="button primary" disabled={disabled} onClick={() => void save(confirm, false)}>Confirm unassign</button></div>
    </div>}
    {busy && <Loading label={phase === "refresh" ? "Change saved. Refreshing listing…" : "Saving assignment…"} />}
    {error && <Feedback error title={phase === "refresh" ? "Listing refresh failed" : "Unable to change assignment"}>{error}</Feedback>}
    {phase === "refresh" && !busy && <button className="button secondary" onClick={() => void retryRead()}>Retry listing refresh</button>}
    {phase === "done" && <Feedback title="Assignments saved">The current assignments have been reloaded from the server.</Feedback>}
  </div>;
}
