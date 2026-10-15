// Isolated browser fixtures only. Production/dev application always uses the real API.
import { test, expect, type Page } from "@playwright/test";
const token = () =>
  `header.${Buffer.from(JSON.stringify({ exp: Date.now() / 1000 + 3600 })).toString("base64url")}.signature`;
const property = {
  id: 1,
  title: "A welcoming family home",
  description: "Bright interiors and a private garden.",
  street: "5 Ridgemont Close",
  city: "Cherrybrook",
  state: "NSW",
  postCode: 2126,
  longitude: 151.04,
  latitude: -33.72,
  price: 1500000,
  bedrooms: 4,
  bathrooms: 2,
  garages: 2,
  floorArea: 250,
  propertyType: 1,
  saleCategory: 1,
  listingStatus: 3,
  agents: [
    {
      id: "agent",
      agentFirstName: "Alex",
      agentLastName: "Chen",
      companyName: "Example Realty",
      avatarUrl: "",
    },
  ],
  caseContacts: [
    {
      contactId: 1,
      firstName: "Sam",
      lastName: "Lee",
      companyName: "Example Realty",
      profileUrl: "",
      email: "sam@example.test",
      phoneNumber: "+61 400 000 000",
    },
  ],
  mediaAssets: [
    {
      id: 1,
      mediaType: 1,
      mediaUrl: "https://media.example.test/house.svg",
      isSelected: true,
      isHero: true,
      uploadedAt: "2026-10-15T00:00:00Z",
    },
    {
      id: 2,
      mediaType: 3,
      mediaUrl: "https://media.example.test/plan.pdf",
      isSelected: false,
      isHero: false,
    },
    {
      id: 3,
      mediaType: 4,
      mediaUrl: "https://media.example.test/tour",
      isSelected: false,
      isHero: false,
    },
    {
      id: 4,
      mediaType: 2,
      mediaUrl: "https://media.example.test/watch",
      isSelected: false,
      isHero: false,
    },
    {
      id: 5,
      mediaType: 90,
      mediaUrl: "javascript:alert(1)",
      isSelected: false,
      isHero: false,
    },
  ],
};
async function setup(page: Page, role = "Agent") {
  await page.route("https://media.example.test/**", (route) =>
    route.fulfill({
      contentType: "image/svg+xml",
      body: '<svg xmlns="http://www.w3.org/2000/svg" width="800" height="530"><rect width="800" height="530" fill="#ddd6ca"/><path d="M170 300L400 100 630 300V470H170Z" fill="#a28f78"/><path d="M355 470V320H445V470" fill="#f3eee6"/></svg>',
    }),
  );
  await page.route("**/api/User/me", (route) =>
    route.fulfill({
      json: {
        success: true,
        data: { id: "test-user", roles: [role], listingCaseIds: [1] },
      },
    }),
  );
  await page.route("**/api/ListingCase/listings?*", (route) => {
    const pageNumber = Number(
      new URL(route.request().url()).searchParams.get("pageNumber"),
    );
    return route.fulfill({
      json: {
        success: true,
        data: {
          pageNumber,
          pageSize: 10,
          totalCount: 11,
          listingCases: [
            pageNumber === 1
              ? property
              : { ...property, id: 11, title: "Second page property" },
          ],
        },
      },
    });
  });
  await page.route("**/api/ListingCase/listings/1", (route) =>
    route.fulfill({ json: { success: true, data: property } }),
  );
  await page.addInitScript(
    (value) => sessionStorage.setItem("remp.token", value),
    token(),
  );
}
async function noOverflow(page: Page) {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
}
test("anonymous protection, labelled login and connection failure", async ({
  page,
}) => {
  await page.goto("/listings/1");
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByLabel("Email address")).toBeVisible();
  await expect(page.getByLabel("Password")).toBeVisible();
  await page.getByLabel("Email address").fill("user@example.test");
  await page.getByLabel("Password").fill("wrong-password");
  await page.route("**/api/Auth/login", (route) =>
    route.abort("connectionrefused"),
  );
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Unable to connect");
  await noOverflow(page);
  await page.screenshot({
    path: test.info().outputPath("login.png"),
    fullPage: true,
  });
});
test("email login calls me, restores on reload and signs out", async ({
  page,
}) => {
  await setup(page, "Admin");
  await page.goto("/login");
  await page.getByRole("button", { name: "Sign out" }).click();
  await page.route("**/api/Auth/login", async (route) => {
    expect(route.request().postDataJSON()).toEqual({
      email: "admin@example.test",
      password: "test-password",
    });
    await route.fulfill({
      json: {
        success: true,
        data: {
          token: token(),
          userName: "admin",
          email: "admin@example.test",
          roles: ["User"],
        },
      },
    });
  });
  await page.getByLabel("Email address").fill("admin@example.test");
  await page.getByLabel("Password").fill("test-password");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(
    page.getByRole("link", { name: "Company listings" }),
  ).toBeVisible();
  await expect(page).toHaveURL(/\/listings$/);
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Listings", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/login$/);
  expect(
    await page.evaluate(() => sessionStorage.getItem("remp.token")),
  ).toBeNull();
});
test("real page components paginate, navigate, render read-only detail and return", async ({
  page,
}) => {
  await setup(page);
  const calls: string[] = [];
  page.on("request", (req) => {
    if (req.url().includes("/api/ListingCase/")) calls.push(req.url());
  });
  await page.goto("/listings");
  await expect(
    page.getByRole("link", { name: "Assigned listings" }),
  ).toBeVisible();
  await noOverflow(page);
  await page.screenshot({
    path: test.info().outputPath("listings.png"),
    fullPage: true,
  });
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(page).toHaveURL(/page=2/);
  await expect(page.getByText("Second page property")).toBeVisible();
  await page.getByRole("button", { name: "Previous", exact: true }).click();
  await page
    .getByRole("link", { name: "View A welcoming family home" })
    .click();
  await expect(
    page.getByRole("heading", { name: "5 Ridgemont Close" }),
  ).toBeVisible();
  await expect(page.getByRole("heading", { name: "Sam Lee" })).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Final display images" }),
  ).toBeVisible();
  await expect(page.getByRole("heading", { name: "VR tours" })).toBeVisible();
  expect(await page.locator("iframe").count()).toBe(0);
  expect(await page.locator('a[href^="javascript:"]').count()).toBe(0);
  expect(calls.filter((url) => /\/listings\/1$/.test(url))).toHaveLength(1);
  expect(
    calls.some((url) => /contacts|final-selection|\/media/.test(url)),
  ).toBe(false);
  await noOverflow(page);
  await page.screenshot({
    path: test.info().outputPath("detail.png"),
    fullPage: true,
  });
  await page.getByRole("link", { name: "Back to listings" }).click();
  await expect(
    page.getByRole("heading", { name: "Listings", exact: true }),
  ).toBeVisible();
});
test("empty list, error and retry", async ({ page }) => {
  await setup(page);
  let fail = true;
  await page.route("**/api/ListingCase/listings?*", (route) =>
    fail
      ? route.fulfill({ status: 500, body: "" })
      : route.fulfill({
          json: {
            success: true,
            data: {
              pageNumber: 1,
              pageSize: 10,
              totalCount: 0,
              listingCases: [],
            },
          },
        }),
  );
  await page.goto("/listings");
  await expect(page.getByRole("alert")).toContainText("unavailable");
  fail = false;
  await page.getByRole("button", { name: "Try again" }).click();
  await expect(page.getByText("No listings yet")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Next", exact: true }),
  ).toBeDisabled();
  await noOverflow(page);
});
test("detail 403 retains session, 404 is readable, 401 redirects to login", async ({
  page,
}) => {
  await setup(page);
  await page.route("**/api/ListingCase/listings/1", (route) =>
    route.fulfill({ status: 403, body: "" }),
  );
  await page.goto("/listings/1");
  await expect(page.getByRole("alert")).toContainText("Access denied");
  expect(
    await page.evaluate(() => sessionStorage.getItem("remp.token")),
  ).toBeTruthy();
  await page.route("**/api/ListingCase/listings/1", (route) =>
    route.fulfill({ status: 404, json: { Message: "Listing missing" } }),
  );
  await page.getByRole("button", { name: "Try again" }).click();
  await expect(page.getByRole("alert")).toContainText("Listing missing");
  await page.route("**/api/ListingCase/listings/1", (route) =>
    route.fulfill({ status: 401, body: "" }),
  );
  await page.getByRole("button", { name: "Try again" }).click();
  await expect(page).toHaveURL(/\/login$/);
});
test("expired sessions never reveal private pages", async ({ page }) => {
  await page.addInitScript(() =>
    sessionStorage.setItem(
      "remp.token",
      `h.${btoa(JSON.stringify({ exp: 1 }))}.s`,
    ),
  );
  await page.goto("/listings");
  await expect(page).toHaveURL(/\/login$/);
  await expect(
    page.getByRole("heading", { name: "Listings", exact: true }),
  ).toHaveCount(0);
});

test("unsupported role shows forbidden and sign out returns to login", async ({ page }) => {
  await setup(page, "User");
  await page.goto("/listings");
  await expect(page).toHaveURL(/\/forbidden$/);
  await expect(page.getByRole("alert")).toContainText("Access denied");
  expect(await page.evaluate(() => sessionStorage.getItem("remp.token"))).toBeTruthy();
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/login$/);
});

test("empty-body login rejection has a credential error, not a session message", async ({ page }) => {
  await page.route("**/api/Auth/login", route => route.fulfill({ status: 401, body: "" }));
  await page.goto("/login");
  await page.getByLabel("Email address").fill("user@example.test");
  await page.getByLabel("Password").fill("incorrect");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Check your email, password");
  await expect(page).toHaveURL(/\/login$/);
});
