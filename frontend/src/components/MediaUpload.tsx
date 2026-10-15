import { useEffect, useRef, useState } from "react";
import { normalizeError, request } from "../api/client";
import { Feedback } from "./Feedback";
import { MiB, uploadTypes, validateUpload } from "./uploadRules";

function Preview({ file }: { file: File }) {
  const [url, setUrl] = useState("");
  const ext = file.name.split(".").pop()?.toLowerCase() || "";
  const image = ["jpg", "jpeg", "png", "webp", "gif"].includes(ext);
  const video = ["mp4", "webm"].includes(ext);
  useEffect(() => {
    if (!image && !video) return;
    const value = URL.createObjectURL(file);
    setUrl(value);
    return () => URL.revokeObjectURL(value);
  }, [file, image, video]);
  return url ? image ? <img src={url} alt={`Preview of ${file.name}`} /> : <video src={url} controls preload="metadata" aria-label={`Preview of ${file.name}`} /> : null;
}
export function MediaUpload({ listingId, refresh }: { listingId: string; refresh: () => Promise<void> }) {
  const [type, setType] = useState(1);
  const [files, setFiles] = useState<File[]>([]);
  const [busy, setBusy] = useState(false);
  const [checking, setChecking] = useState(false);
  const [progress, setProgress] = useState<number | null>(null);
  const [error, setError] = useState("");
  const [phase, setPhase] = useState<"idle" | "refresh" | "done">("idle");
  const lock = useRef(false);
  const mounted = useRef(true);
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => {
    if (!busy) return;
    const prevent = (event: BeforeUnloadEvent) => { event.preventDefault(); };
    window.addEventListener("beforeunload", prevent);
    return () => window.removeEventListener("beforeunload", prevent);
  }, [busy]);
  const rule = uploadTypes.find(t => t.value === type)!;
  async function choose(incoming: File[]) {
    if (lock.current || phase === "refresh") return;
    lock.current = true; setBusy(true); setChecking(true); setError(""); setPhase("idle");
    try {
      const next = type === 1 ? [...files, ...incoming] : incoming;
      const problem = await validateUpload(next, type);
      if (!mounted.current) return;
      if (problem) setError(problem); else setFiles(next);
    } catch { if (mounted.current) setError("Unable to read the selected file. Please choose it again."); }
    finally { lock.current = false; if (mounted.current) { setBusy(false); setChecking(false); } }
  }
  async function readDetail() {
    try {
      await refresh();
      if (mounted.current) { setPhase("done"); setError(""); }
    } catch (e) {
      if (mounted.current) setError(`Upload confirmed, but the gallery could not be refreshed. ${normalizeError(e).message}`);
    }
  }
  async function submit() {
    if (lock.current || !files.length || phase === "refresh") return;
    lock.current = true; setBusy(true); setError(""); setProgress(null);
    try {
      const body = new FormData();
      files.forEach(file => body.append("files", file));
      body.append("type", String(type)); body.append("listingCaseId", listingId);
      await request("/MediaAsset/media/upload", { method: "POST", data: body, timeout: 0,
        onUploadProgress: event => { if (mounted.current) setProgress(event.total ? Math.min(100, Math.round(event.loaded / event.total * 100)) : null); },
      });
      if (!mounted.current) return;
      setFiles([]); if (input.current) input.current.value = "";
      setPhase("refresh"); setProgress(null);
      await readDetail();
    } catch (e) {
      if (mounted.current) setError(`${normalizeError(e).message} Your selection is retained. If the connection failed, check the gallery before retrying; the server may have received the upload.`);
    } finally { lock.current = false; if (mounted.current) setBusy(false); }
  }
  async function retryRead() {
    if (lock.current) return;
    lock.current = true; setBusy(true); await readDetail(); lock.current = false; if (mounted.current) setBusy(false);
  }
  return <section className="detail-section upload-section" aria-labelledby="upload-heading">
    <div className="section-title"><h2 id="upload-heading">Upload media</h2></div>
    <p className="muted">Add original files to this listing. Removing a pending file only changes this selection.</p>
    <fieldset disabled={busy || phase === "refresh"}>
      <label htmlFor="upload-type">Media type</label>
      <select id="upload-type" value={type} onChange={e => { setType(Number(e.target.value)); setFiles([]); setError(""); setPhase("idle"); if (input.current) input.current.value = ""; }}>
        {uploadTypes.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
      </select>
      {type === 4 ? <p role="status">VR uploads are unavailable until the file format is agreed. Existing VR resources remain viewable.</p> : <>
        <p id="upload-help">{rule.accept} · Up to {rule.limit} MiB per file. {type === 1 ? "Up to 20 pictures, 100 MiB per batch." : "One file per upload."}</p>
        <div className="upload-drop" onDragOver={e => e.preventDefault()} onDrop={e => { e.preventDefault(); if (!busy) void choose(Array.from(e.dataTransfer.files)); }}>
          <label htmlFor="upload-files">Choose files or drag and drop here</label>
          <input ref={input} id="upload-files" type="file" accept={rule.accept} multiple={type === 1} aria-describedby="upload-help" onChange={e => { const chosen = Array.from(e.target.files || []); e.target.value = ""; if (chosen.length) void choose(chosen); }} />
        </div>
        <ul className="pending-media">{files.map((file, index) => <li key={`${file.name}-${file.lastModified}-${index}`}>
          <Preview file={file} /><div><strong>{file.name}</strong><p>{(file.size / MiB).toFixed(2)} MiB ({file.size.toLocaleString()} bytes)</p></div>
          <button className="button secondary" type="button" onClick={() => { setFiles(files.filter((_, i) => i !== index)); setError(""); }} aria-label={`Remove pending ${file.name}`}>Remove</button>
        </li>)}</ul>
        <button className="button primary" type="button" disabled={!files.length} onClick={() => void submit()}>{checking ? "Checking files…" : busy ? "Uploading…" : "Upload selected files"}</button>
      </>}
    </fieldset>
    {busy && <div role="status" aria-live="polite"><progress aria-label="Upload progress" max="100" value={progress ?? undefined} /><p>{checking ? "Checking selected files…" : phase === "refresh" ? "Upload confirmed. Refreshing gallery…" : progress === 100 ? "100% transferred. Waiting for server confirmation…" : progress === null ? "Preparing or transferring files…" : `${progress}% transferred`}</p></div>}
    {error && <Feedback error title={phase === "refresh" ? "Gallery refresh failed" : "Unable to upload"}>{error}</Feedback>}
    {phase === "refresh" && !busy && <button className="button secondary" onClick={() => void retryRead()}>Retry gallery refresh</button>}
    {phase === "done" && <Feedback title="Media uploaded">The gallery now shows the saved resources.</Feedback>}
  </section>;
}
