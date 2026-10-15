import type { Listing } from "../api/types";
export const statuses: Record<number, string> = {
  1: "Created",
  2: "Pending",
  3: "Delivered",
};
export const sales: Record<number, string> = {
  1: "For sale",
  2: "For rent",
  3: "Auction",
};
export const properties: Record<number, string> = {
  1: "House",
  2: "Unit",
  3: "Townhouse",
  4: "Villa",
  5: "Other",
};
export const address = (item: Listing) =>
  [item.street, item.city, item.state, item.postCode || ""]
    .filter(Boolean)
    .join(", ");
export function safeUrl(value: string): string | undefined {
  try {
    const url = new URL(value);
    return ["http:", "https:"].includes(url.protocol) &&
      !url.username &&
      !url.password
      ? url.href
      : undefined;
  } catch {
    return undefined;
  }
}
