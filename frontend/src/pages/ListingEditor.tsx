import { useEffect, useRef, useState, type ChangeEvent, type FormEvent } from "react";
import { Link, useBlocker, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { readToken } from "../auth/session";
import { ApiError, normalizeError, request } from "../api/client";
import type { Listing } from "../api/types";
import { useResource } from "../api/useResource";
import { ErrorState, Feedback, Loading } from "../components/Feedback";
import { formValues, labels, validateListing, type FormErrors, type FormValues } from "../components/listingForm";
import { properties, sales } from "../components/property";

export function ListingEditor({ edit = false }: { edit?: boolean }) {
  const { user } = useAuth();
  const { id } = useParams();
  if (!user?.roles.includes("Admin")) return <Feedback error title="Access denied">Only company administrators can create or edit listings.</Feedback>;
  if (!edit) return <ListingForm key="new" />;
  if (!id || !/^[1-9]\d*$/.test(id) || Number(id) > 2147483647) return <Feedback error title="Listing not found"><Link to="/listings">Back to listings</Link></Feedback>;
  return <EditLoader id={id} key={id} />;
}
function EditLoader({ id }: { id: string }) {
  const { data, error, retry } = useResource<Listing>(`/ListingCase/listings/${id}`);
  return <>
    <Link className="back-link" to={`/listings/${id}`}>← Back to listing</Link>
    {error ? <ErrorState error={error} retry={retry} /> : !data ? <Loading label="Loading listing for editing…" /> : data.listingStatus === 3 ?
      <Feedback title="Delivered listing is read-only">This listing has been delivered and cannot be edited.</Feedback> : <ListingForm key={data.id} listing={data} />}
  </>;
}
function ListingForm({ listing }: { listing?: Listing }) {
  const navigate = useNavigate();
  const initial = useRef(formValues(listing));
  const [values, setValues] = useState<FormValues>(initial.current);
  const [errors, setErrors] = useState<FormErrors>({});
  const [error, setError] = useState<ApiError>();
  const [saving, setSaving] = useState(false);
  const busy = useRef(false);
  const clean = useRef(false);
  const mounted = useRef(true);
  const form = useRef<HTMLFormElement>(null);
  const dirty = JSON.stringify(values) !== JSON.stringify(initial.current);
  const dirtyRef = useRef(dirty);
  dirtyRef.current = dirty;
  const { status } = useAuth();
  useBlocker(() => status === "authenticated" && !clean.current &&
    (busy.current ? (window.alert("Please wait for the save request to finish."), true) : dirtyRef.current && !window.confirm("Discard unsaved listing changes?")));
  useEffect(() => {
    mounted.current = true;
    const beforeUnload = (event: BeforeUnloadEvent) => {
      if (!clean.current && (dirtyRef.current || busy.current)) { event.preventDefault(); event.returnValue = ""; }
    };
    const beforeSignout = (event: Event) => {
      if (busy.current) { window.alert("Please wait for the save request to finish."); event.preventDefault(); }
      else if (dirtyRef.current && !window.confirm("Discard unsaved listing changes and sign out?")) event.preventDefault();
    };
    window.addEventListener("remp:before-signout", beforeSignout);
    window.addEventListener("beforeunload", beforeUnload);
    return () => { mounted.current = false; window.removeEventListener("beforeunload", beforeUnload); window.removeEventListener("remp:before-signout", beforeSignout); };
  }, []);
  const back = listing ? `/listings/${listing.id}` : "/listings";
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy.current) return;
    const result = validateListing(values);
    setErrors(result.errors);
    setError(undefined);
    if (!result.data) {
      const key = Object.keys(result.errors)[0];
      form.current?.querySelector<HTMLElement>(`[name="${key}"]`)?.focus();
      return;
    }
    busy.current = true;
    setSaving(true);
    const token = readToken();
    try {
      const response = await request<{ id: number }>(listing ? `/ListingCase/listings/${listing.id}` : "/ListingCase/listings", { method: listing ? "PUT" : "POST", data: result.data });
      if (!mounted.current || token !== readToken()) return;
      const id = listing?.id ?? response?.id;
      if (!Number.isInteger(id) || id <= 0) throw new ApiError(0, "The API did not return a valid listing ID. Check the listing list before trying again.");
      clean.current = true;
      // The destination always fetches the persisted detail. No optimistic success data.
      navigate(`/listings/${id}`, { replace: true, state: { listingSaved: true } });
    } catch (failure) {
      if (mounted.current && token === readToken()) setError(normalizeError(failure));
    } finally {
      busy.current = false;
      if (mounted.current) setSaving(false);
    }
  }
  const field = (key: keyof FormValues, type = "text", icon?: string) => {
    const invalid = errors[key];
    const common = { id: `listing-${key}`, name: key, value: values[key], "aria-invalid": !!invalid, "aria-describedby": invalid ? `error-${key}` : undefined,
      onChange: (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => setValues(current => ({ ...current, [key]: event.target.value })) };
    return <div className={`form-field ${key === "description" || key === "title" || key === "street" ? "wide" : ""}`} key={key}>
      <label htmlFor={common.id}>{icon && <img src={`/icons/form/${icon}.svg`} alt="" />}{labels[key]}{key !== "description" && <span aria-hidden="true"> *</span>}</label>
      {key === "description" ? <textarea {...common} rows={5} /> : key === "propertyType" || key === "saleCategory" ?
        <select {...common} required><option value="">Choose…</option>{Object.entries(key === "propertyType" ? properties : sales).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select> :
        <input {...common} type={type} required maxLength={key === "title" ? 255 : undefined} step={type === "number" ? (["postCode", "bedrooms", "bathrooms", "garages"].includes(key) ? "1" : "any") : undefined} />}
      {invalid && <p className="field-error" id={`error-${key}`}>{invalid}</p>}
    </div>;
  };
  return <div className="listing-editor">
    <div className="page-heading"><div><p className="eyebrow">PROPERTY LIBRARY</p><h1>{listing ? "Edit listing" : "Create listing"}</h1><p className="muted">Review and complete the property details. Fields marked * are required.</p></div></div>
    <form ref={form} onSubmit={submit} noValidate aria-busy={saving}>
      <fieldset disabled={saving}>
        <section className="form-section"><h2>Property information</h2><div className="form-grid">{field("title")}{field("description")}</div></section>
        <section className="form-section"><h2>Property address</h2><div className="form-grid">{field("street")}{field("city")}{field("state")}{field("postCode", "number")}{field("latitude", "number")}{field("longitude", "number")}</div></section>
        <section className="form-section"><h2>Sale and property type</h2><div className="form-grid">{field("saleCategory")}{field("propertyType")}{field("price", "number")}</div></section>
        <section className="form-section"><h2>Basic information</h2><div className="form-grid">{field("bedrooms", "number", "beds")}{field("bathrooms", "number", "baths")}{field("garages", "number", "parking")}{field("floorArea", "number", "area")}</div></section>
      </fieldset>
      {Object.keys(errors).length > 0 && <div className="inline-error" role="alert">Check the highlighted fields before saving.</div>}
      {error && <Feedback error title={error.status === 403 ? "Access denied — listing was not saved" : error.status === 404 ? "Listing not found — listing was not saved" : "Unable to save listing"}>{error.message}</Feedback>}
      <div className="form-actions"><button className="button secondary" type="button" disabled={saving} onClick={() => navigate(back)}>Cancel</button><button className="button primary" type="submit" disabled={saving}>{saving ? "Saving…" : listing ? "Save changes" : "Create listing"}</button></div>
    </form>
  </div>;
}
