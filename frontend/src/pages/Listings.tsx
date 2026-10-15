import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import type { ListingPage } from "../api/types";
import { useResource } from "../api/useResource";
import { ErrorState, Feedback, Loading } from "../components/Feedback";
import { address, sales, statuses } from "../components/property";
export function Listings() {
  const [params, setParams] = useSearchParams();
  const raw = Number(params.get("page") || 1);
  const page = Number.isSafeInteger(raw) && raw > 0 && raw <= 1000000 ? raw : 1;
  const { user } = useAuth();
  const { data, error, retry } = useResource<ListingPage>(
    `/ListingCase/listings?pageNumber=${page}&pageSize=10`,
  );
  const pages = data
    ? Math.max(1, Math.ceil(data.totalCount / data.pageSize))
    : 1;
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">PROPERTY LIBRARY</p>
          <h1>Listings</h1>
          <p className="muted">
            {user?.roles.includes("Admin")
              ? "Property listings belonging to your company."
              : "Property listings assigned to you."}
          </p>
        </div>
        <div className="heading-actions">{data && <span className="count">{data.totalCount} listings</span>}
          {user?.roles.includes("Admin") && <Link className="button primary" to="/listings/new">Create listing</Link>}</div>
      </div>
      {error ? (
        <ErrorState error={error} retry={retry} />
      ) : !data ? (
        <Loading label="Loading listings…" />
      ) : (
        <>
          {data.listingCases.length === 0 ? (
            <Feedback
              title={page > 1 ? "No listings on this page" : "No listings yet"}
            >
              {page > 1
                ? "Return to the first page to see available listings."
                : "Listings will appear here when they become available to your account."}
            </Feedback>
          ) : (
            <div className="listing-table">
              <div className="table-heading">
                <span>PROPERTY</span>
                <span>DELIVERY STATUS</span>
                <span>SALE METHOD</span>
                <span />
              </div>
              {data.listingCases.map((item) => (
                <article className="listing-row" key={item.id}>
                  <div>
                    <p className="listing-number">LISTING {item.id}</p>
                    <h2>
                      <Link to={`/listings/${item.id}`}>
                        {item.title || item.street || `Listing ${item.id}`}
                      </Link>
                    </h2>
                    <p className="muted">
                      {address(item) || "Address not provided"}
                    </p>
                    <p className="facts">
                      {item.bedrooms} beds <span>·</span> {item.bathrooms} baths{" "}
                      <span>·</span> {item.garages} parking
                    </p>
                  </div>
                  <div>
                    <span className={`badge status-${item.listingStatus}`}>
                      {statuses[item.listingStatus] || "Unknown status"}
                    </span>
                  </div>
                  <div className="sale-label">
                    {sales[item.saleCategory] || "Not specified"}
                  </div>
                  <Link
                    className="text-link"
                    to={`/listings/${item.id}`}
                    aria-label={`View ${item.title || `listing ${item.id}`}`}
                  >
                    View listing →
                  </Link>
                </article>
              ))}
            </div>
          )}
          <nav className="pagination" aria-label="Listing pages">
            <p>
              {data.totalCount > 0 && page <= pages
                ? `${(page - 1) * data.pageSize + 1}–${Math.min(page * data.pageSize, data.totalCount)} of ${data.totalCount}`
                : "0 results"}
              <span>
                {" "}
                · Page {page} of {pages}
              </span>
            </p>
            <div>
              {page > pages && (
                <button
                  className="button secondary compact"
                  onClick={() => setParams({ page: "1" })}
                >
                  First page
                </button>
              )}
              <button
                className="button secondary compact"
                disabled={page === 1}
                onClick={() => setParams({ page: String(page - 1) })}
              >
                Previous
              </button>
              <button
                className="button secondary compact"
                disabled={page >= pages}
                onClick={() => setParams({ page: String(page + 1) })}
              >
                Next
              </button>
            </div>
          </nav>
        </>
      )}
    </>
  );
}
