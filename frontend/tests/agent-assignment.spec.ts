// Isolated HTTP fixtures verify UI behavior; not real SQL/JWT acceptance.
import { test, expect, type Page } from "@playwright/test";
const alex = { id: "agent-a", agentFirstName: "Alex", agentLastName: "Chen", email: "alex@example.test", companyName: "Example Realty", avatarUrl: "" };
const sam = { ...alex, id: "agent-b", agentFirstName: "Sam", email: "sam@example.test" };
async function setup(page: Page, role = "Admin", candidates = [alex, sam], assigned = [alex]) {
  const token = `header.${Buffer.from(JSON.stringify({ exp: Date.now() / 1000 + 3600 })).toString("base64url")}.signature`;
  await page.addInitScript(value => sessionStorage.setItem("remp.token", value), token);
  await page.route("**/api/User/me", route => route.fulfill({ json: { success: true, data: { id: "owner", roles: [role] } } }));
  const record = { id: 42, title: "Assignment acceptance", street: "Test street", listingStatus: 3, price: 0, bedrooms: 0, bathrooms: 0, garages: 0, floorArea: 0, propertyType: 1, saleCategory: 1, agents: assigned, mediaAssets: [] };
  await page.route("**/api/ListingCase/listings/42", route => route.fulfill({ json: { success: true, data: record } }));
  let reads = 0;
  await page.route("**/api/User/agents", route => { reads++; return route.fulfill({ json: { success: true, data: candidates } }); });
  await page.goto("/listings/42");
  await expect(page.getByRole("heading", { name: record.title })).toBeVisible();
  return { record, reads: () => reads };
}
const manage = (page: Page) => page.getByRole("button", { name: "Manage agents" });
const panel = (page: Page) => page.locator("#agent-assignment");
test("company candidates, multiple assignments, duplicate suppression, confirmation and persistence on Delivered", async ({ page }) => {
  const { record, reads } = await setup(page); let writes = 0;
  expect(reads()).toBe(0);
  await page.route("**/api/ListingCase/listings/42/agents/*", async route => {
    writes++;
    expect(route.request().headers().authorization).toContain("Bearer");
    expect(route.request().postData()).toBeNull();
    await new Promise(resolve => setTimeout(resolve, 350));
    if (route.request().method() === "PUT") record.agents = [alex, sam];
    else record.agents = record.agents.filter(a => a.id !== alex.id);
    await route.fulfill({ json: { success: true, data: { listingCaseId: 42, agentId: sam.id, assigned: true } } });
  });
  await manage(page).click();
  await expect(panel(page).getByText(alex.email, { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Assigned Alex Chen", exact: true })).toBeDisabled();
  const assign = page.getByRole("button", { name: "Assign Sam Chen", exact: true });
  await assign.focus(); await page.keyboard.press("Enter");
  await expect(assign).toBeDisabled(); await assign.evaluate(el => el.dispatchEvent(new MouseEvent("click", { bubbles: true })));
  // Collapsing during a write must not reset the lock or lose the pending refresh.
  await manage(page).click(); await manage(page).click();
  await expect(panel(page).getByText("Assignments saved", { exact: true })).toBeVisible(); expect(writes).toBe(1);
  await expect(page.getByRole("button", { name: "Assigned Sam Chen", exact: true })).toBeDisabled();
  await page.getByRole("button", { name: "Unassign Alex Chen", exact: true }).click();
  await page.getByRole("button", { name: "Cancel", exact: true }).click(); expect(writes).toBe(1);
  await page.getByRole("button", { name: "Unassign Alex Chen", exact: true }).click();
  await page.getByRole("button", { name: "Confirm unassign", exact: true }).click();
  await expect(page.getByRole("button", { name: "Assign Alex Chen", exact: true })).toBeEnabled(); expect(writes).toBe(2);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await panel(page).screenshot({ path: test.info().outputPath("agent-assignment.png") });
  await page.reload();
  await expect(page.locator(".people-grid").getByRole("heading", { name: "Sam Chen" })).toBeVisible();
  await expect(page.locator(".people-grid").getByRole("heading", { name: "Alex Chen" })).toHaveCount(0);
});
test("empty company and Agent view never fetches candidates", async ({ page }) => {
  const state = await setup(page, "Agent");
  await expect(manage(page)).toHaveCount(0); expect(state.reads()).toBe(0);
  await expect(page.getByRole("heading", { name: "Alex Chen" })).toBeVisible();
  await setup(page, "Admin", [], []); await manage(page).click();
  await expect(page.getByRole("heading", { name: "No company agents" })).toBeVisible();
});
test("candidate load failure and retry", async ({ page }) => {
  await setup(page);
  await page.route("**/api/User/agents", route => route.fulfill({ status: 500, body: "" }));
  await manage(page).click(); await expect(panel(page).getByRole("alert")).toBeVisible();
  await page.route("**/api/User/agents", route => route.fulfill({ json: { success: true, data: [sam] } }));
  await panel(page).getByRole("button", { name: "Try again" }).click();
  await expect(panel(page).getByText(sam.email, { exact: true })).toBeVisible();
});
for (const method of ["PUT", "DELETE"]) for (const code of [400, 401, 403, 404, 409, 500, 0]) test(`${method} error ${code} preserves assignments`, async ({ page }) => {
  await setup(page); let writes = 0;
  await page.route("**/api/ListingCase/listings/42/agents/*", route => { writes++; expect(route.request().method()).toBe(method); return code ? route.fulfill({ status: code, body: "" }) : route.abort("connectionrefused"); });
  await manage(page).click();
  if (method === "PUT") await page.getByRole("button", { name: "Assign Sam Chen", exact: true }).click();
  else { await page.getByRole("button", { name: "Unassign Alex Chen", exact: true }).click(); await page.getByRole("button", { name: "Confirm unassign", exact: true }).click(); }
  if (code === 401) { await expect(page).toHaveURL(/\/login$/); return; }
  await expect(panel(page).getByRole("alert")).toContainText("Displayed assignments have not been changed");
  await expect(page.getByRole("button", { name: "Assigned Alex Chen", exact: true })).toBeDisabled();
  await expect(page.locator(".people-grid").getByRole("heading", { name: "Alex Chen" })).toBeVisible(); expect(writes).toBe(1);
});
test("confirmed write with failed detail refresh only retries GET", async ({ page }) => {
  const { record } = await setup(page); let writes = 0, fail = true;
  await page.route("**/api/ListingCase/listings/42/agents/*", route => { writes++; record.agents = [alex, sam]; return route.fulfill({ json: { success: true, data: {} } }); });
  await page.route("**/api/ListingCase/listings/42", route => fail ? route.fulfill({ status: 500, body: "" }) : route.fulfill({ json: { success: true, data: record } }));
  await manage(page).click(); await page.getByRole("button", { name: "Assign Sam Chen", exact: true }).click();
  await expect(panel(page).getByRole("alert")).toContainText("Change saved");
  await expect(page.getByRole("button", { name: "Assign Sam Chen", exact: true })).toBeDisabled();
  await expect(page.locator(".people-grid").getByRole("heading", { name: "Sam Chen" })).toHaveCount(0);
  await manage(page).click(); await manage(page).click();
  fail = false; await page.getByRole("button", { name: "Retry listing refresh" }).click();
  await expect(page.getByRole("button", { name: "Assigned Sam Chen", exact: true })).toBeDisabled(); expect(writes).toBe(1);
});
for (const stage of ["write", "read"]) for (const leave of ["listing", "logout"]) test(`late ${stage} cannot replace state after ${leave}`, async ({ page }) => {
  await setup(page);
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/ListingCase/listings/42/agents/*", async route => { if (stage === "write") await pending; await route.fulfill({ json: { success: true, data: {} } }).catch(() => {}); });
  if (stage === "read") await page.route("**/api/ListingCase/listings/42", async route => { await pending; await route.fulfill({ json: { success: true, data: { id: 42, title: "Stale listing", agents: [sam] } } }).catch(() => {}); });
  await page.route("**/api/ListingCase/listings/43", route => route.fulfill({ json: { success: true, data: { id: 43, title: "Other listing", price: 0, agents: [] } } }));
  await manage(page).click(); await page.getByRole("button", { name: "Assign Sam Chen", exact: true }).click();
  await expect(panel(page).getByText(stage === "write" ? "Saving assignment…" : "Change saved. Refreshing listing…", { exact: true })).toBeVisible();
  if (leave === "logout") await page.getByRole("button", { name: /sign out/i }).click();
  else await page.evaluate(() => { history.pushState({}, "", "/listings/43"); dispatchEvent(new PopStateEvent("popstate")); });
  release();
  if (leave === "logout") await expect(page).toHaveURL(/\/login$/);
  else { await expect(page.getByRole("heading", { name: "Other listing", level: 1 })).toBeVisible(); await expect(page.getByRole("heading", { name: "No agents assigned" })).toBeVisible(); }
  await expect(page.getByText("Assignments saved", { exact: true })).toHaveCount(0);
});
