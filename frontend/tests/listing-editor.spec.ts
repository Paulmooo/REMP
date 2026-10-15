// Isolated API fixtures; never loaded by the application.
import { test, expect, type Page } from "@playwright/test";
const property = { id: 42, title: "Garden home", description: "A private garden.", street: "12 Main Street", city: "Sydney", state: "NSW", postCode: 2000, longitude: 151.04, latitude: -33.72, price: 123456.78, bedrooms: 0, bathrooms: 2, garages: 0, floorArea: 56.7, propertyType: 5, saleCategory: 3, listingStatus: 2, agents: [], caseContacts: [], mediaAssets: [] };
async function setup(page: Page, role = "Admin", status = 2) {
  const token = `header.${Buffer.from(JSON.stringify({ exp: Date.now() / 1000 + 3600 })).toString("base64url")}.signature`;
  await page.addInitScript(value => sessionStorage.setItem("remp.token", value), token);
  await page.route("**/api/User/me", route => route.fulfill({ json: { success: true, data: { id: "owner", roles: [role], listingCaseIds: [42] } } }));
  const record = { ...property, listingStatus: status };
  await page.route("**/api/ListingCase/listings?*", route => route.fulfill({ json: { success: true, data: { totalCount: 1, pageSize: 10, pageNumber: 1, listingCases: [record] } } }));
  await page.route("**/api/ListingCase/listings/42", async route => {
    if (route.request().method() === "PUT") {
      Object.assign(record, route.request().postDataJSON());
      await route.fulfill({ json: { success: true, data: { id: 42 } } });
    } else await route.fulfill({ json: { success: true, data: record } });
  });
  return record;
}
async function fill(page: Page) {
  for (const [name, value] of Object.entries({ Title: "New home", "Street address": "12 Main Street", "City / suburb": "Sydney", "State / territory": "NSW", Postcode: "2000", Latitude: "0", Longitude: "0", "Listed price (AUD)": "0", Bedrooms: "0", Bathrooms: "0", Garages: "0", "Floor area (m²)": "0" })) await page.getByLabel(name, { exact: false }).fill(value);
  await page.getByLabel("Property type").selectOption("2");
  await page.getByLabel("Sale method").selectOption("3");
}
async function screenshot(page: Page, name: string) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: test.info().outputPath(name), fullPage: true });
}
test("Admin creates using returned ID, then edits all fields and reads persisted detail", async ({ page }) => {
  const record = await setup(page);
  let posts = 0, puts = 0;
  page.on("request", request => { if (request.method() === "PUT") puts++; });
  await page.route("**/api/ListingCase/listings", async route => {
    posts++;
    const body = route.request().postDataJSON();
    expect(Object.keys(body)).toHaveLength(15);
    expect(body).toMatchObject({ price: 0, bedrooms: 0, latitude: 0, propertyType: 2, saleCategory: 3 });
    Object.assign(record, body);
    await new Promise(resolve => setTimeout(resolve, 350));
    await route.fulfill({ json: { success: true, data: { id: 42, createdAt: "2026-10-15T00:00:00Z" } } });
  });
  await page.goto("/listings");
  await page.getByRole("link", { name: "Create listing", exact: true }).click();
  await fill(page);
  await page.getByLabel("Property description").fill("New description");
  await screenshot(page, "create.png");
  await page.getByRole("button", { name: "Create listing", exact: true }).click();
  await expect(page.getByRole("button", { name: "Saving…" })).toBeDisabled();
  // A second programmatic submit during the request must not produce another POST.
  await page.locator("form").evaluate(form => form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })));
  await expect(page).toHaveURL(/\/listings\/42$/);
  await expect(page.getByRole("status").filter({ hasText: "Listing saved" })).toBeVisible();
  expect(posts).toBe(1);
  await page.getByRole("link", { name: "Edit listing", exact: true }).click();
  for (const [key, value] of Object.entries(record)) {
    const input = page.locator(`[name="${key}"]`);
    if (await input.count()) await expect(input).toHaveValue(String(value));
  }
  await page.getByLabel("Title", { exact: false }).fill("Updated persisted title");
  await page.getByLabel("Latitude").fill("-33.72");
  await page.getByLabel("Listed price").fill("123.45");
  await screenshot(page, "edit.png");
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(page).toHaveURL(/\/listings\/42$/);
  await expect(page.getByRole("heading", { name: "Updated persisted title" })).toBeVisible();
  expect(record.latitude).toBe(-33.72);
  expect(record.price).toBe(123.45);
  expect(puts).toBe(1);
});
test("edit prefill preserves every editable field including decimals, zero and enum values", async ({ page }) => {
  await setup(page);
  let body: unknown;
  await page.route("**/api/ListingCase/listings/42", route => {
    if (route.request().method() === "PUT") { body = route.request().postDataJSON(); return route.fulfill({ json: { success: true, data: { id: 42 } } }); }
    return route.fulfill({ json: { success: true, data: property } });
  });
  await page.goto("/listings/42/edit");
  await expect(page.getByLabel("Title", { exact: false })).toHaveValue(property.title);
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(page).toHaveURL(/\/listings\/42$/);
  const { id, listingStatus, agents, caseContacts, mediaAssets, ...editable } = property;
  expect(body).toEqual(editable);
});
test("Agent has no create/edit entries and direct routes are blocked without detail reads", async ({ page }) => {
  await setup(page, "Agent");
  await page.goto("/listings");
  await expect(page.getByRole("heading", { name: "Listings", exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: "Create listing" })).toHaveCount(0);
  await page.goto("/listings/42");
  await expect(page.getByRole("heading", { name: property.title })).toBeVisible();
  await expect(page.getByRole("link", { name: "Edit listing" })).toHaveCount(0);
  let reads = 0;
  page.on("request", request => { if (request.url().endsWith("/listings/42")) reads++; });
  for (const url of ["/listings/new", "/listings/42/edit"]) {
    await page.goto(url);
    await expect(page.getByRole("alert")).toContainText("Only company administrators");
    await expect(page.locator("form")).toHaveCount(0);
  }
  expect(reads).toBe(0);
});
test("Delivered listing has no edit entry and direct edit is read-only", async ({ page }) => {
  await setup(page, "Admin", 3);
  await page.goto("/listings/42");
  await expect(page.getByText("Delivered listing is read-only.")).toBeVisible();
  await expect(page.getByRole("link", { name: "Edit listing" })).toHaveCount(0);
  await page.goto("/listings/42/edit");
  await expect(page.getByRole("status")).toContainText("cannot be edited");
  await expect(page.locator("form")).toHaveCount(0);
});
test("validation, failed save, retry and navigation prompts preserve input", async ({ page }) => {
  await setup(page);
  let writes = 0;
  await page.route("**/api/ListingCase/listings", route => { writes++; return route.fulfill({ status: 400, json: { Message: "Server rejected this listing." } }); });
  await page.goto("/listings/new");
  await page.getByRole("button", { name: "Create listing", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Check the highlighted fields");
  await expect(page.getByLabel("Title", { exact: false })).toBeFocused();
  expect(writes).toBe(0);
  await fill(page);
  await page.getByLabel("Postcode").fill("0");
  await page.getByLabel("Bedrooms").fill("1.5");
  await page.getByLabel("Listed price").fill("-1");
  await page.getByLabel("Longitude").fill("");
  await page.getByRole("button", { name: "Create listing", exact: true }).click();
  expect(writes).toBe(0);
  await expect(page.locator('[aria-invalid="true"]')).toHaveCount(4);
  await fill(page);
  await page.getByRole("button", { name: "Create listing", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Server rejected");
  await expect(page.getByLabel("Title", { exact: false })).toHaveValue("New home");
  await expect(page).toHaveURL(/\/listings\/new$/);
  page.once("dialog", dialog => dialog.dismiss());
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await expect(page).toHaveURL(/\/listings\/new$/);
  page.once("dialog", dialog => dialog.dismiss());
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page.getByLabel("Title", { exact: false })).toHaveValue("New home");
  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("link", { name: "Company listings" }).click();
  await expect(page).toHaveURL(/\/listings$/);
  expect(writes).toBe(1);
});
for (const code of [401, 403, 404, 0]) test(`edit read handles ${code || "network failure"}`, async ({ page }) => {
  await setup(page);
  await page.route("**/api/ListingCase/listings/42", route => code ? route.fulfill({ status: code, body: "" }) : route.abort("connectionrefused"));
  await page.goto("/listings/42/edit");
  if (code === 401) await expect(page).toHaveURL(/\/login$/);
  else { await expect(page.getByRole("alert")).toContainText(code === 403 ? "Access denied" : code === 404 ? "Not found" : "Unable to connect"); await expect(page.locator("form")).toHaveCount(0); }
});
for (const code of [401, 403, 404, 0]) test(`edit save handles ${code || "network failure"} without false success`, async ({ page }) => {
  await setup(page);
  await page.route("**/api/ListingCase/listings/42", route => route.request().method() === "GET" ? route.fulfill({ json: { success: true, data: property } }) : code ? route.fulfill({ status: code, body: "" }) : route.abort("connectionrefused"));
  await page.goto("/listings/42/edit");
  await page.getByLabel("Title", { exact: false }).fill("Unsaved change");
  await page.getByRole("button", { name: "Save changes" }).click();
  if (code === 401) await expect(page).toHaveURL(/\/login$/);
  else { await expect(page.getByRole("alert")).toBeVisible(); await expect(page.getByLabel("Title", { exact: false })).toHaveValue("Unsaved change"); await expect(page.getByRole("button", { name: "Save changes" })).toBeEnabled(); }
  await expect(page.getByText("Listing saved", { exact: true })).toHaveCount(0);
});
test("a Delivered rejection after load preserves edits and can be retried explicitly", async ({ page }) => {
  const record = await setup(page);
  let writes = 0;
  await page.route("**/api/ListingCase/listings/42", route => {
    if (route.request().method() === "GET") return route.fulfill({ json: { success: true, data: record } });
    writes++;
    if (writes === 1) return route.fulfill({ status: 400, json: { Message: "Listing case is delivered and cannot be updated." } });
    Object.assign(record, route.request().postDataJSON());
    return route.fulfill({ json: { success: true, data: { id: 42 } } });
  });
  await page.goto("/listings/42/edit");
  await page.getByLabel("Title", { exact: false }).fill("Retained edit");
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(page.getByRole("alert")).toContainText("delivered and cannot be updated");
  await expect(page.getByLabel("Title", { exact: false })).toHaveValue("Retained edit");
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(page).toHaveURL(/\/listings\/42$/);
  await expect(page.getByRole("heading", { name: "Retained edit" })).toBeVisible();
  expect(writes).toBe(2);
});
test("failed detail read after save offers read retry without resubmitting or false success", async ({ page }) => {
  await setup(page);
  let writes = 0, saved = false;
  await page.route("**/api/ListingCase/listings/42", route => {
    if (route.request().method() === "PUT") { writes++; saved = true; return route.fulfill({ json: { success: true, data: { id: 42 } } }); }
    if (saved) return route.fulfill({ status: 500, body: "" });
    return route.fulfill({ json: { success: true, data: property } });
  });
  await page.goto("/listings/42/edit");
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(page).toHaveURL(/\/listings\/42$/);
  await expect(page.getByRole("alert")).toContainText("service is unavailable");
  await expect(page.getByText("Listing saved", { exact: true })).toHaveCount(0);
  await page.getByRole("button", { name: "Try again" }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  expect(writes).toBe(1);
});
test("browser back can retain changes, then discard; edit cancel returns to its detail", async ({ page }) => {
  await setup(page);
  await page.goto("/listings");
  await page.getByRole("link", { name: "Create listing", exact: true }).click();
  await page.getByLabel("Title", { exact: false }).fill("Keep this draft");
  page.once("dialog", dialog => dialog.dismiss());
  await page.goBack();
  await expect(page.getByLabel("Title", { exact: false })).toHaveValue("Keep this draft");
  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await expect(page).toHaveURL(/\/listings$/);
  await page.goto("/listings/42/edit");
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await expect(page).toHaveURL(/\/listings\/42$/);
});
