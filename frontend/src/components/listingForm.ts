import type { Listing } from "../api/types";
import { properties, sales } from "./property";

export const textFields = ["title", "description", "street", "city", "state"] as const;
export const numericFields = ["postCode", "price", "bedrooms", "bathrooms", "garages", "floorArea", "latitude", "longitude", "propertyType", "saleCategory"] as const;
export type ListingInput = Pick<Listing, typeof textFields[number] | typeof numericFields[number]>;
export type FormValues = Record<keyof ListingInput, string>;
export type FormErrors = Partial<Record<keyof ListingInput, string>>;
export const labels: Record<keyof ListingInput, string> = {
  title: "Title", description: "Property description", street: "Street address", city: "City / suburb", state: "State / territory",
  postCode: "Postcode", price: "Listed price (AUD)", bedrooms: "Bedrooms", bathrooms: "Bathrooms", garages: "Garages", floorArea: "Floor area (m²)",
  latitude: "Latitude", longitude: "Longitude", propertyType: "Property type", saleCategory: "Sale method",
};
export function formValues(listing?: Listing): FormValues {
  return Object.fromEntries([...textFields, ...numericFields].map(key => [key, listing?.[key] == null ? "" : String(listing[key])])) as FormValues;
}
const integers: readonly string[] = ["postCode", "bedrooms", "bathrooms", "garages"];
export function validateListing(values: FormValues): { errors: FormErrors; data?: ListingInput } {
  const errors: FormErrors = {};
  for (const key of ["title", "street", "city", "state"] as const) {
    if (!values[key].trim()) errors[key] = `${labels[key]} is required.`;
  }
  if (values.title.length > 255) errors.title = "Title cannot exceed 255 characters.";
  for (const key of numericFields) {
    const raw = values[key].trim();
    const number = Number(raw);
    if (!raw) errors[key] = `${labels[key]} is required. Enter 0 if applicable.`;
    else if (!Number.isFinite(number)) errors[key] = `Enter a valid number for ${labels[key].toLowerCase()}.`;
    else if (integers.includes(key) && (!Number.isInteger(number) || number > 2147483647)) errors[key] = "Enter a whole number up to 2147483647.";
    else if (key === "postCode" && number <= 0) errors[key] = "Postcode must be a positive integer.";
    else if (!["latitude", "longitude"].includes(key) && number < 0) errors[key] = `${labels[key]} cannot be negative.`;
    else if (key === "propertyType" && !Object.hasOwn(properties, number)) errors[key] = "Choose a property type.";
    else if (key === "saleCategory" && !Object.hasOwn(sales, number)) errors[key] = "Choose a sale method.";
    // API decimal coordinates have no geographic range Validator; enforce only its decimal magnitude here.
    else if (["latitude", "longitude"].includes(key) && Math.abs(number) > 79228162514264337593543950335) errors[key] = "This coordinate exceeds the API numeric range.";
  }
  if (Object.keys(errors).length) return { errors };
  return { errors, data: Object.fromEntries([
    ...textFields.map(key => [key, values[key].trim()]),
    ...numericFields.map(key => [key, Number(values[key])]),
  ]) as unknown as ListingInput };
}
