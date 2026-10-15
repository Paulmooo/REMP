// Isolated API fixtures; these checks do not claim real Blob persistence.
import { test, expect, type Page } from "@playwright/test";
const property = { id: 42, title: "Upload acceptance", street: "12 Test Street", city: "Sydney", state: "NSW", listingStatus: 2, price: 0, bedrooms: 0, bathrooms: 0, garages: 0, floorArea: 0, propertyType: 1, saleCategory: 1, mediaAssets: [] as object[] };
const png = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aG7sAAAAASUVORK5CYII=", "base64");
const image = (name = "photo.png") => ({ name, mimeType: "image/png", buffer: png });
async function setup(page: Page, role = "Admin", status = 2) {
  const token = `header.${Buffer.from(JSON.stringify({ exp: Date.now() / 1000 + 3600 })).toString("base64url")}.signature`;
  await page.addInitScript(value => sessionStorage.setItem("remp.token", value), token);
  await page.route("**/api/User/me", route => route.fulfill({ json: { success: true, data: { id: "owner", roles: [role] } } }));
  const record = { ...property, listingStatus: status, mediaAssets: [] as object[] };
  await page.route("**/api/ListingCase/listings/42", route => route.fulfill({ json: { success: true, data: record } }));
  await page.goto("/listings/42");
  await expect(page.getByRole("heading", { name: property.title })).toBeVisible();
  return record;
}
const upload = (page: Page) => page.getByRole("button", { name: "Upload selected files" });
test("multi-picture previews, remove, progress, no duplicates, persisted gallery after reopen", async ({ page }) => {
  await page.addInitScript(() => {
    const create = URL.createObjectURL.bind(URL), revoke = URL.revokeObjectURL.bind(URL);
    const active = new Set<string>();
    Object.assign(window, { pendingPreviewUrls: active });
    URL.createObjectURL = value => { const url = create(value); active.add(url); return url; };
    URL.revokeObjectURL = value => { active.delete(value); revoke(value); };
  });
  const record = await setup(page);
  let writes = 0;
  await page.route("**/api/MediaAsset/media/upload", async route => {
    writes++;
    expect(route.request().headers()["authorization"]).toContain("Bearer");
    expect(route.request().headers()["content-type"]).toMatch(/multipart\/form-data; boundary=/);
    const body = route.request().postDataBuffer()!.toString("latin1");
    expect(body).toContain('name="type"\r\n\r\n1'); expect(body).toContain('name="listingCaseId"\r\n\r\n42');
    expect(body).toContain('filename="second.png"'); expect(body).not.toContain('filename="first.png"');
    await new Promise(resolve => setTimeout(resolve, 1200));
    record.mediaAssets = [{ id: 1, mediaType: 1, mediaUrl: "https://media.test/saved.png" }];
    await route.fulfill({ json: { success: true, data: record.mediaAssets } });
  });
  await page.route("https://media.test/saved.png", route => route.fulfill({ contentType: "image/png", body: png }));
  await page.getByLabel("Choose files or drag and drop here").setInputFiles([image("first.png"), image("second.png")]);
  await expect(page.getByAltText("Preview of second.png")).toBeVisible();
  await page.getByRole("button", { name: "Remove pending first.png" }).click();
  await expect(page.getByAltText("Preview of first.png")).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as { pendingPreviewUrls: Set<string> }).pendingPreviewUrls.size)).toBe(1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: test.info().outputPath("pending-upload.png"), fullPage: true });
  await upload(page).click();
  await expect(page.getByRole("progressbar", { name: "Upload progress" })).toBeVisible();
  await expect(page.getByLabel("Media type")).toBeDisabled();
  await page.getByRole("button", { name: "Uploading…" }).evaluate(el => el.dispatchEvent(new MouseEvent("click", { bubbles: true })));
  await expect(page.getByRole("heading", { name: "Media uploaded", exact: true })).toBeVisible();
  await expect(page.getByAltText("Image 1")).toBeVisible();
  expect(writes).toBe(1);
  expect(await page.evaluate(() => (window as unknown as { pendingPreviewUrls: Set<string> }).pendingPreviewUrls.size)).toBe(0);
  await page.locator('#upload-files').setInputFiles(image("unmount.png"));
  await expect(page.getByAltText("Preview of unmount.png")).toBeVisible();
  await page.getByRole("link", { name: "Edit listing", exact: true }).click();
  expect(await page.evaluate(() => (window as unknown as { pendingPreviewUrls: Set<string> }).pendingPreviewUrls.size)).toBe(0);
  await page.goto("/listings/42");
  await page.reload(); await expect(page.getByAltText("Image 1")).toBeVisible();
});
test("single-file types, validation, drag and drop and VR format notice", async ({ page }) => {
  await setup(page);
  await page.getByLabel("Media type").selectOption("2");
  expect(await page.locator('#upload-files').getAttribute("multiple")).toBeNull();
  await page.locator('.upload-drop').evaluate(el => {
    const dt = new DataTransfer();
    for (const name of ["one.mp4", "two.mp4"]) dt.items.add(new File(["0000ftypisom"], name, { type: "video/mp4" }));
    el.dispatchEvent(new DragEvent("drop", { bubbles: true, dataTransfer: dt }));
  });
  await expect(page.getByRole("alert")).toContainText("only one file");
  await page.getByLabel("Media type").selectOption("1");
  await page.locator('#upload-files').setInputFiles({ name: "fake.png", mimeType: "image/png", buffer: Buffer.from("fake") });
  await expect(page.getByRole("alert")).toContainText("contents do not match");
  await page.locator('#upload-files').setInputFiles({ name: "bad.exe", mimeType: "image/png", buffer: png });
  await expect(page.getByRole("alert")).toContainText("type is not allowed");
  await page.locator('#upload-files').evaluate(el => {
    const dt = new DataTransfer(); dt.items.add(new File([new Uint8Array(20 * 1024 * 1024 + 1)], "large.png"));
    (el as HTMLInputElement).files = dt.files; el.dispatchEvent(new Event("change", { bubbles: true }));
  });
  await expect(page.getByRole("alert")).toContainText("at most 20 MiB");
  await page.locator('.upload-drop').evaluate((el, bytes) => {
    const dt = new DataTransfer(); dt.items.add(new File([new Uint8Array(bytes)], "dropped.png", { type: "image/png" }));
    el.dispatchEvent(new DragEvent("drop", { bubbles: true, dataTransfer: dt }));
  }, [...png]);
  await expect(page.getByAltText("Preview of dropped.png")).toBeVisible();
  await page.getByLabel("Media type").selectOption("4");
  await expect(page.getByText("VR uploads are unavailable", { exact: false })).toBeVisible();
  await expect(upload(page)).toHaveCount(0);
});
for (const type of [2, 3]) test(`single media type ${type} uploads using correct form field`, async ({ page }) => {
  await setup(page);
  let writes = 0;
  await page.route("**/api/MediaAsset/media/upload", route => {
    writes++;
    expect(route.request().postDataBuffer()!.toString("latin1")).toContain(`name="type"\r\n\r\n${type}`);
    return route.fulfill({ json: { success: true, data: [] } });
  });
  await page.getByLabel("Media type").selectOption(String(type));
  await page.locator('#upload-files').setInputFiles(type === 2 ? { name: "clip.mp4", mimeType: "video/mp4", buffer: Buffer.from("0000ftypisom0") } : { name: "plan.pdf", mimeType: "application/pdf", buffer: Buffer.from("%PDF-1.7\n") });
  if (type === 2) await expect(page.getByLabel("Preview of clip.mp4")).toBeVisible();
  await upload(page).click();
  await expect(page.getByRole("heading", { name: "Media uploaded", exact: true })).toBeVisible(); expect(writes).toBe(1);
});
for (const code of [400, 401, 403, 404, 413, 500, 0]) test(`upload error ${code || "network"} preserves selection for explicit retry`, async ({ page }) => {
  await setup(page); let writes = 0;
  await page.route("**/api/MediaAsset/media/upload", route => { writes++; return code ? route.fulfill({ status: code, body: "" }) : route.abort("connectionrefused"); });
  await page.locator('#upload-files').setInputFiles(image()); await upload(page).click();
  if (code === 401) { await expect(page).toHaveURL(/\/login$/); return; }
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page.getByAltText("Preview of photo.png")).toBeVisible(); await expect(upload(page)).toBeEnabled();
  await page.route("**/api/MediaAsset/media/upload", route => { writes++; return route.fulfill({ json: { success: true, data: [] } }); });
  await upload(page).click(); await expect(page.getByRole("heading", { name: "Media uploaded", exact: true })).toBeVisible(); expect(writes).toBe(2);
});
test("confirmed upload with failed refresh retries only GET", async ({ page }) => {
  await setup(page); let writes = 0, failRead = true;
  await page.route("**/api/MediaAsset/media/upload", route => { writes++; return route.fulfill({ json: { success: true, data: [] } }); });
  await page.route("**/api/ListingCase/listings/42", route => failRead ? route.fulfill({ status: 500, body: "" }) : route.fulfill({ json: { success: true, data: property } }));
  await page.locator('#upload-files').setInputFiles(image()); await upload(page).click();
  await expect(page.getByRole("alert")).toContainText("Upload confirmed");
  await expect(page.getByAltText("Preview of photo.png")).toHaveCount(0); await expect(upload(page)).toBeDisabled();
  failRead = false; await page.getByRole("button", { name: "Retry gallery refresh" }).click();
  await expect(page.getByRole("heading", { name: "Media uploaded", exact: true })).toBeVisible(); expect(writes).toBe(1);
});
test("Agent has no upload controls; Delivered Admin retains existing upload permission", async ({ page }) => {
  await setup(page, "Agent"); await expect(page.getByRole("heading", { name: "Upload media" })).toHaveCount(0);
  await setup(page, "Admin", 3); await expect(page.getByRole("heading", { name: "Upload media" })).toBeVisible();
});
