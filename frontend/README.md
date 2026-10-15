# REMP frontend

React + TypeScript + Vite, TailwindCSS, React Router and Axios. Identity uses Context and `sessionStorage`; all application requests use the real backend. Test fixtures exist only in test files.

## Start

Use Node.js 22.12+ (verified with 22.22.1) and npm. From the repository root:

```bash
cd frontend
npm ci
cp .env.example .env.local
npm run dev
```

Open http://127.0.0.1:5173. In a separate terminal, configure the backend as described in the root README, then run:

```bash
dotnet run --project Remp.API/Remp.API.csproj --launch-profile http
```

The existing HTTP launch profile uses `http://localhost:5265`. Vite proxies `/api` there, so development does not require a backend CORS change. Change `API_PROXY_TARGET` in `.env.local` if your API listens elsewhere. Restart Vite after changing environment variables.

`VITE_API_BASE_URL` is public browser configuration and defaults to `/api`. For production it must point to a reachable API URL (including `/api`) or a same-origin reverse proxy. An absolute cross-origin URL requires the API operator to configure CORS. Never put JWT signing keys, SQL credentials or Blob connection strings in any frontend variable. `API_PROXY_TARGET` is development-server configuration, not a browser variable.

A configured SQL Server-backed API and an existing password-enabled Admin or Agent account are needed for real login. Public registration is disabled, company-created passwordless Agents cannot yet log in, and there is no demo initializer or activation flow. No account is automatically created by this frontend.

## Pages and behavior

- `/login`: email/password validation, readable failures, login followed by `GET /api/User/me`.
- `/listings` (including `/listings/`): server pagination, delivery status, sale method, address and property fields. No unsupported search, filters or sorting.
- `/listings/new`: Admin-only shared Listing form. POST creates a Listing and opens the returned ID.
- `/listings/:id/edit`: Admin-only full editable-field prefill, PUT and fresh detail read after saving. Delivered Listings show a read-only restriction.
- `/listings/:id`: property details and an Admin edit entry when editable; read-only agents, contacts, final selected images/cover, image/video/floor plan/VR resources. Reuses the detail response without separate media/contact/selection requests.
- `/forbidden` and authenticated unknown routes: readable access/not-found states.

Admin navigation shows **Company listings**; Agent navigation shows **Assigned listings**. `me.roles` determines access, and backend authorization determines which records are returned. `me` currently exposes ID and roles rather than a display name/email, so the header shows user ID and role. No frontend permission expansion is performed.

Only the token is persisted under `sessionStorage['remp.token']`. On reload its JWT expiry is checked before calling `me`; the decoded token is never an authority for roles. Expiry, focus after expiry and authenticated HTTP 401 clear the session. HTTP 403 preserves it. Network errors during restoration keep private content hidden and offer retry. There is no refresh-token flow.

Media URLs must be absolute HTTP(S) URLs without embedded credentials. Images and directly recognizable video files can be embedded; unsupported video, VR, PDF floor plans and unknown media have explicit links. There are no arbitrary iframes. Remote resource access still depends on backend Blob permissions/URL availability. Failed images show a fallback with the resource link.

## Verify and build

```bash
npm run typecheck
npm test
npm run build
npm run test:browser
npm run preview
```

`build` produces `dist/`. `preview` is a local build preview, not a deployment server, and does not provide the development API proxy. A production host needs SPA fallback to `index.html` and an API reverse proxy or explicit `VITE_API_BASE_URL`; deployment remains out of scope.

Unit tests cover identity restoration, protected routes and API errors. Browser tests use installed Google Chrome, build/serve production assets on port 5173, and intercept API calls only inside the test runner. Stop the development server before running browser tests. Desktop (1440px) and mobile (390px) checks cover login/restoration/logout, pagination, detail navigation, empty/error/retry states and 401/403/404 handling. Reports/screenshots are under ignored `test-results/`. Fixtures do not prove real database/JWT integration.

## Design reference and current limits

The supplied Figma file `ofk35IrEyrN6Y9yy8PYyjG`, node `2199:448` (delivery detail), was accessible after the link update. The detail page follows its light canvas, white navigation, brown property hero, media groups and contact cards. Four original property-stat SVGs are stored in `public/icons/`, preserving their intrinsic dimensions. Property photos and avatars come from the API. Login/listing layouts extend that visual language; their specific Figma screens were not inspected. The result adapts the detail composition to an authenticated, responsive, read-only workflow rather than claiming full pixel reproduction.

No upload, deletion, Agent assignment, status changes, selection editing, contact creation, account management, Dashboard, activity history, download, sharing, registration or deployment UI is included. Real Admin login and Listing creation/edit persistence were accepted on 2026-10-16 using the local API and the explicitly authorized pm Admin account. Real Agent/Delivered/media scenarios still need suitable accounts, data and accessible media URLs.

## Listing create/edit acceptance

From the Admin list choose **Create listing**; from an editable detail choose **Edit listing**. Agent accounts have neither entry and cannot open either form route. The form sends exactly the 15 fields in the current Create/Update DTO. All numeric fields need an explicit value (0 is allowed where the API permits it); empty input is never converted to zero. Description is optional, title is limited to 255 characters, postcode is a positive int32, room counts are nonnegative int32, price/area are nonnegative finite numbers, and enum values follow the existing mappings. Coordinates accept negative/zero values as permitted by the current Validator. Price retains the existing **Listed price (AUD)** meaning. Media, contacts, assignment and delivery status are not part of this form.

Saving disables the form and guards duplicate submissions. Errors preserve inputs; 401 clears the session, while 403/404/network failures display errors. Success navigates to detail and fetches persisted data; a failed detail read offers GET retry without repeating the write. Cancel returns to the list (create) or detail (edit). Dirty forms use a browser confirmation on application navigation/back/sign-out and a beforeunload prompt on refresh/close. Save requests must finish before ordinary application navigation/sign-out.

The Figma Property Details (`2:2781`) and Property description (`2:3235`) screens were inspected with screenshots. Their sections, light inputs, buttons and four original 36px icons inform the responsive standalone form; address search, modal reproduction and rich text are outside this batch.

Latest verification: **23 unit tests**, **48 browser tests** (desktop/mobile, including 32 new create/edit checks), **14 existing Listing service tests**, typecheck and production build passed. Four form screenshots were visually checked. Fixtures stay in tests only. Real Admin acceptance passed on 2026-10-16: after the user started localhost:5265 and authorized a one-time local Admin account (test@pm.com, company pm), real browser login → empty list → create → returned detail → edit → persisted detail → page reload all succeeded. All 15 fields were checked on edit, including zero values, negative coordinates, decimals and enum changes. Local acceptance Listing 1 remains available, clearly named LOCAL ACCEPTANCE 2026-10-16 · pm · EDITED. Real Agent/cross-company/Delivered scenarios and real mobile persistence remain pending; no extra Agent or state changes were introduced for verification. The existing desktop/mobile fixture checks cover those UI restrictions. No demo initializer was added.
