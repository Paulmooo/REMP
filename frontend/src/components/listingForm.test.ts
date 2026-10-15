import { describe, it, expect } from "vitest";
import { formValues, validateListing } from "./listingForm";
const valid = () => ({ ...formValues(), title: "Home", street: "1 Main St", city: "Sydney", state: "NSW", description: "", postCode: "2000", price: "0", bedrooms: "0", bathrooms: "0", garages: "0", floorArea: "0", latitude: "0", longitude: "0", propertyType: "1", saleCategory: "3" });
describe("Listing DTO form conversion", () => {
  it("preserves explicit zero and submits only the 15 editable API fields with numeric enums", () => {
    const { data, errors } = validateListing(valid());
    expect(errors).toEqual({});
    expect(data).toMatchObject({ price: 0, bedrooms: 0, latitude: 0, propertyType: 1, saleCategory: 3 });
    expect(Object.keys(data!)).toHaveLength(15);
  });
  it("rejects empty numeric input without converting it to zero", () => {
    const values = valid(); values.price = " "; values.longitude = "";
    expect(validateListing(values).errors).toHaveProperty("price");
    expect(validateListing(values).data).toBeUndefined();
  });
  it("checks required text, title length, integer bounds, finite values and API enum mappings", () => {
    const values = { ...valid(), title: "a".repeat(256), city: " ", postCode: "0", bedrooms: "0.5", bathrooms: "2147483648", garages: "-1", price: "NaN", floorArea: "Infinity", propertyType: "6", saleCategory: "0" };
    expect(Object.keys(validateListing(values).errors).sort()).toEqual(["title", "city", "postCode", "bedrooms", "bathrooms", "garages", "price", "floorArea", "propertyType", "saleCategory"].sort());
  });
  it("allows negative coordinates, decimals and an optional description", () => {
    const { data } = validateListing({ ...valid(), latitude: "-33.72", longitude: "151.04", price: "123.45", floorArea: "56.7" });
    expect(data).toMatchObject({ latitude: -33.72, longitude: 151.04, price: 123.45, floorArea: 56.7, description: "" });
  });
});
