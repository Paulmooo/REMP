import { useState } from "react";
import type { Media } from "../api/types";
import { safeUrl } from "./property";
export function ResourceImage({
  url,
  alt,
  className = "",
}: {
  url: string;
  alt: string;
  className?: string;
}) {
  const [failed, setFailed] = useState(false);
  const source = safeUrl(url);
  return source && !failed ? (
    <img
      className={className}
      src={source}
      alt={alt}
      loading="lazy"
      onError={() => setFailed(true)}
    />
  ) : (
    <div className={`image-placeholder ${className}`}>Image unavailable</div>
  );
}
export function MediaCard({ item }: { item: Media }) {
  const url = safeUrl(item.mediaUrl);
  const type =
    (
      { 1: "Image", 2: "Video", 3: "Floor plan", 4: "VR tour" } as Record<
        number,
        string
      >
    )[item.mediaType] || "Resource";
  const path = url ? new URL(url).pathname.toLowerCase() : "";
  const image =
    item.mediaType === 1 ||
    (item.mediaType === 3 && /\.(png|jpe?g|webp|gif|avif)$/.test(path));
  const video = item.mediaType === 2 && /\.(mp4|webm|ogv)$/.test(path);
  const [videoError, setVideoError] = useState(false);
  return (
    <figure
      className={`media-card ${item.mediaType === 3 ? "floor-plan" : ""}`}
    >
      {image ? (
        <ResourceImage
          key={item.mediaUrl}
          url={item.mediaUrl}
          alt={`${type} ${item.id}`}
        />
      ) : video && url && !videoError ? (
        <video
          controls
          preload="metadata"
          aria-label={`Video ${item.id}`}
          onError={() => setVideoError(true)}
          src={url}
        />
      ) : (
        <div className="media-placeholder">
          <span>{type}</span>
          <p>
            {item.mediaType === 4
              ? "Open this virtual tour in a new tab."
              : "Use the resource link to view this file."}
          </p>
        </div>
      )}
      <figcaption>
        <div>
          <strong>
            {type} {item.id}
          </strong>
          {item.isHero && <span className="badge">Cover</span>}
          {item.isSelected && <span className="selection-label">Selected</span>}
        </div>
        {url ? (
          <a
            className="text-link"
            href={url}
            target="_blank"
            rel="noopener noreferrer"
          >
            Open resource ↗<span className="sr-only"> {item.id} (new tab)</span>
          </a>
        ) : (
          <span className="muted">Resource URL unavailable</span>
        )}
      </figcaption>
    </figure>
  );
}
