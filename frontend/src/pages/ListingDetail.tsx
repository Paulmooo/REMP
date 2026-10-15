import { useAuth } from "../auth/AuthContext";
import { useState, type ReactNode } from "react";
import { request } from "../api/client";
import { AgentAssignment } from "../components/AgentAssignment";
import { MediaUpload } from "../components/MediaUpload";
import { Link, useLocation, useParams } from "react-router-dom";
import type { Listing, Media } from "../api/types";
import { useResource } from "../api/useResource";
import { ErrorState, Feedback, Loading } from "../components/Feedback";
import { MediaCard, ResourceImage } from "../components/MediaCard";
import {
  address,
  properties,
  safeUrl,
  sales,
  statuses,
} from "../components/property";
function Section({
  title,
  children,
  count,
}: {
  title: string;
  children: ReactNode;
  count?: number;
}) {
  return (
    <section className="detail-section">
      <div className="section-title">
        <h2>{title}</h2>
        {count !== undefined && <span className="count">{count}</span>}
      </div>
      {children}
    </section>
  );
}
function Gallery({ items, empty }: { items: Media[]; empty: string }) {
  return items.length ? (
    <div className="media-grid">
      {items.map((item) => (
        <MediaCard key={item.id} item={item} />
      ))}
    </div>
  ) : (
    <Feedback title={empty} />
  );
}
export function ListingDetail() {
  const { id } = useParams();
  if (!id || !/^[1-9]\d*$/.test(id) || Number(id) > 2147483647)
    return (
      <Feedback error title="Listing not found">
        Choose a valid listing from the <Link to="/listings">listing list</Link>
        .
      </Feedback>
    );
  return <Detail id={id} key={id} />;
}
function Detail({ id }: { id: string }) {
  const { user } = useAuth();
  const location = useLocation();
  const [refreshed, setRefreshed] = useState<Listing>();
  const { data: original, error, retry } = useResource<Listing>(
    `/ListingCase/listings/${id}`,
  );
  const data = refreshed ?? original;
  if (error)
    return (
      <>
        <Link className="back-link" to="/listings">
          ← Back to listings
        </Link>
        <ErrorState error={error} retry={retry} />
      </>
    );
  if (!data) return <Loading label="Loading property details…" />;
  const media = data.mediaAssets || [];
  const selected = media
    .filter((item) => item.mediaType === 1 && item.isSelected)
    .sort((a, b) => a.id - b.id);
  const hero = media.find((item) => item.mediaType === 1 && item.isHero);
  const groups = [
    { type: 1, title: "Images" },
    { type: 3, title: "Floor plans" },
    { type: 2, title: "Videography" },
    { type: 4, title: "VR tours" },
  ];
  const agents = data.agents || [];
  const contacts = data.caseContacts || [];
  return (
    <>
      <Link className="back-link" to="/listings">
        ← Back to listings
      </Link>
      {location.state?.listingSaved && <Feedback title="Listing saved">The persisted listing details are shown below.</Feedback>}
      {user?.roles.includes("Admin") && <div className="detail-actions">{data.listingStatus === 3 ? <p className="muted">Delivered listing details cannot be edited.</p> : <Link className="button secondary" to={`/listings/${id}/edit`}>Edit listing</Link>}</div>}
      <div className="property-hero">
        <div className="hero-image">
          {hero ? (
            <ResourceImage
              key={hero.mediaUrl}
              url={hero.mediaUrl}
              alt={`Cover for ${data.title || data.street}`}
            />
          ) : (
            <div className="image-placeholder">No cover selected</div>
          )}
        </div>
        <div className="hero-info">
          <span className="hero-category">
            {properties[data.propertyType] || "Property"} ·{" "}
            {sales[data.saleCategory] || "Sale method not specified"}
          </span>
          <h1>{data.street || data.title || `Listing ${data.id}`}</h1>
          <p>
            {[data.city, data.state, data.postCode || ""]
              .filter(Boolean)
              .join(" ")}
          </p>
          <span className={`badge status-${data.listingStatus}`}>
            {statuses[data.listingStatus] || "Unknown status"}
          </span>
          <div className="hero-divider" />
          <div className="property-stats">
            {[
              { icon: "beds", text: `${data.bedrooms} Beds` },
              { icon: "baths", text: `${data.bathrooms} Baths` },
              { icon: "parking", text: `${data.garages} Parking` },
              { icon: "area", text: `${data.floorArea} m²` },
            ].map((stat) => (
              <div key={stat.icon}>
                <img src={`/icons/${stat.icon}.svg`} alt="" />
                <span>{stat.text}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
      <Section title="Property information">
        <h3>{data.title || "Untitled listing"}</h3>
        <p className="muted">{address(data)}</p>
        <p className="description">
          {data.description || "No description provided."}
        </p>
        <dl className="property-data">
          <div>
            <dt>Listed price (AUD)</dt>
            <dd>
              {new Intl.NumberFormat("en-AU", {
                style: "currency",
                currency: "AUD",
                maximumFractionDigits: 2,
              }).format(data.price)}
            </dd>
          </div>
          <div>
            <dt>Property type</dt>
            <dd>{properties[data.propertyType] || "Not specified"}</dd>
          </div>
          <div>
            <dt>Sale method</dt>
            <dd>{sales[data.saleCategory] || "Not specified"}</dd>
          </div>
          <div>
            <dt>Coordinates</dt>
            <dd>
              {data.latitude}, {data.longitude}
            </dd>
          </div>
        </dl>
      </Section>
      {user?.roles.includes("Admin") && <MediaUpload listingId={id} refresh={async () => {
        const updated = await request<Listing>(`/ListingCase/listings/${id}`);
        setRefreshed(updated);
      }} />}
      <Section title="Final display images" count={selected.length}>
        <p className="muted section-note">
          The current shared selection. The cover is labelled below.
        </p>
        <Gallery items={selected} empty="No display images selected" />
      </Section>
      {groups.map((group) => {
        const items = media.filter((m) => m.mediaType === group.type);
        return (
          <Section key={group.type} title={group.title} count={items.length}>
            <Gallery
              items={items}
              empty={`No ${group.title.toLowerCase()} available`}
            />
          </Section>
        );
      })}
      {media.some((m) => ![1, 2, 3, 4].includes(m.mediaType)) && (
        <Section title="Other resources">
          <Gallery
            items={media.filter((m) => ![1, 2, 3, 4].includes(m.mediaType))}
            empty="No other resources"
          />
        </Section>
      )}
      <Section title="Assigned agents" count={agents.length}>
        {user?.roles.includes("Admin") && <AgentAssignment listingId={id} agents={agents} refresh={async (signal) => {
          const updated = await request<Listing>(`/ListingCase/listings/${id}`, { signal });
          if (!signal.aborted) setRefreshed(updated);
        }} />}
        {agents.length ? (
          <div className="people-grid">
            {agents.map((agent) => (
              <article className="person-card" key={agent.id}>
                {safeUrl(agent.avatarUrl) && (
                  <ResourceImage
                    url={agent.avatarUrl}
                    alt=""
                    className="avatar"
                  />
                )}
                <div>
                  <h3>
                    {agent.agentFirstName} {agent.agentLastName}
                  </h3>
                  <p className="muted">
                    {agent.companyName || "Company not provided"}
                  </p>
                </div>
              </article>
            ))}
          </div>
        ) : (
          <Feedback title="No agents assigned" />
        )}
      </Section>
      <Section title="Contacts" count={contacts.length}>
        {contacts.length ? (
          <div className="people-grid">
            {contacts.map((contact) => (
              <article className="person-card" key={contact.contactId}>
                {safeUrl(contact.profileUrl) && (
                  <ResourceImage
                    url={contact.profileUrl}
                    alt=""
                    className="avatar"
                  />
                )}
                <div>
                  <h3>
                    {contact.firstName} {contact.lastName}
                  </h3>
                  <p className="muted">
                    {contact.companyName || "Company not provided"}
                  </p>
                  <div className="contact-links">
                    {contact.email && (
                      <a href={`mailto:${encodeURIComponent(contact.email)}`}>
                        {contact.email}
                      </a>
                    )}
                    {contact.phoneNumber && (
                      <a
                        href={`tel:${contact.phoneNumber.replace(/[^+\d]/g, "")}`}
                      >
                        {contact.phoneNumber}
                      </a>
                    )}
                  </div>
                </div>
              </article>
            ))}
          </div>
        ) : (
          <Feedback title="No contacts available" />
        )}
      </Section>
    </>
  );
}
