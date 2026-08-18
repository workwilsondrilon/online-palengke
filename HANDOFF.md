# Handoff — Online Palengke

Read this first in any new session. Also read the full implementation plan at
`C:\Users\Wilson\.claude\plans\let-s-plan-out-the-partitioned-hellman.md` — it has the
locked product decisions (1-24), production decisions (25-36), the full data model, and
the Epic 0-12 roadmap. This file is the **current-state** supplement: what's actually
built, what's mid-flight, and the gotchas a fresh session would otherwise rediscover the
hard way.

## Repo / environment

- `D:\Projects\OnlinePalengke`, git repo at `github.com/workwilsondrilon/online-palengke`,
  branch `develop` (default), `master` is the production-release branch (untouched so far).
- **The user asked for granular commits — one logical change per commit, pushed
  immediately.** This has been followed throughout; keep doing it.
- Local MySQL is the **native Windows install** at `127.0.0.1:3306`, NOT the Docker
  container (which is stopped, not removed — `docker compose up mysql` would double-run
  if started without realizing this). App connects as user `palengke` (scoped to only the
  `onlinepalengke` database) via `dotnet user-secrets` on `OnlinePalengke.Api` — never
  check `appsettings.Development.json` for the real local connection string, it still
  shows the Docker-oriented default for portability to other contributors.
- AWS credentials (a shared "op-user" key used across other unrelated projects in the
  same account — broad S3 access, no IAM management rights) live in the local, gitignored
  `.env` at repo root and were also exported ad hoc in shell sessions. Two S3 buckets are
  live: `op-dev-public-assets` (public GetObject policy) and `op-dev-private` (Block
  Public Access on), both `ap-southeast-1`.
- JWT signing key is in `dotnet user-secrets` on `OnlinePalengke.Api` (`Jwt:SigningKey`).
- A test admin user exists in the local DB: `admin@onlinepalengke.test` / `TestPass123!`
  (id 3). Two other ad hoc test users (id 1, 2) also exist from earlier manual testing —
  harmless, ignorable.
- GitHub Actions CI is green on `develop` as of commit `31a52ba`. Two workflows exist
  beyond CI: `build-and-push.yml` (triggers on push to `master`, not yet exercised —
  nothing has merged to `master` yet) and `deploy.yml` (manual `workflow_dispatch` only,
  will fail until Epic 10 provisions the VPS and adds the `production` secrets —
  expected, not a bug).
- **Still pending, needs the user's action, not something a session can do alone**:
  GitHub Environments (`ci`, `production`) haven't been created yet — see plan Epic 1.

## What's done (verified, committed, pushed)

- **Epic 0 (Foundation)**: full backend skeleton under `admin/src/`, Docker+Traefik local
  dev stack, S3 upload pipeline, Blazor admin shell (16 placeholder pages), 3 Flutter app
  shells, `palengke_core` shared package. 44+ backend unit tests, 22 Flutter tests.
- **Epic 1 (CI/CD)**: GitHub repo, 3 Actions workflows (path-filtered), S3 buckets
  provisioned and round-trip verified against real AWS.
- **Epic 2 (Real Identity & Auth) — closed.** Phone-OTP + JWT fully implemented and
  verified live end-to-end (request → verify → authenticated call → refresh → rotate →
  replay-rejected → signout → idempotent-signout → cross-role-conflict-rejected). Admin
  email+password login also implemented and verified (correct login, wrong password,
  nonexistent email all behave correctly, no user-enumeration).
  `DevHeaderAuthenticationHandler` and its `DenyAllAuthenticationHandler` fallback are
  **deleted** (not just gated) — `ApiSetup.AddApiAuthentication` now registers JWT Bearer
  as the API's only scheme, satisfying Epic 2's exit criterion literally. Confirmed live:
  a request with no bearer token 401s, a request carrying the old
  `X-Dev-User-Id`/`X-Dev-Role` headers *with no bearer token* also 401s (proving those
  headers are no longer honored at all, not just less privileged), and a real admin JWT
  passes auth on the same protected endpoint. `Auth:EnableDevHeaderScheme` is gone from
  both `appsettings.json` files and from `docker-compose.yml`.

  **SMS sending is still stubbed** — `LoggingSmsSender` in Infrastructure logs the OTP
  code instead of sending a real SMS. This was explicit, requested scope ("Provision OTP
  for now... no actual OTP sent") and remains a deliberate gap against the plan's literal
  Epic 2 exit criterion ("a real phone number receives a real OTP... on a device"). The
  chosen provider is decided — **m360** — but not yet integrated; swap the one DI
  registration in `OnlinePalengke.Infrastructure/DependencyInjection.cs` for a real m360
  adapter when the user is ready; nothing above `ISmsSender` needs to change.

  In the meantime, an **admin-configurable OTP verification bypass** now exists so this
  gap doesn't block QA/demos: `OtpVerificationSettings` (Domain/Identity), a single-row
  `otp_verification_settings` table (migration `003_otp_verification_settings.sql`,
  singleton-row pattern — `id` pinned to 1 by a CHECK, no seed row, a missing row means
  bypass is off), `OtpSettingsService`, and two new admin-only endpoints —
  `GET`/`PUT /api/admin/auth/otp-settings`. When enabled, `OtpAuthService.VerifyOtpAsync`
  skips *only* the hashed-code-correctness check — the OTP row still has to genuinely
  exist, be unexpired, and not have exceeded its attempt budget — and logs a warning per
  bypassed verification. Verified live end-to-end: default state (no row) reports
  `bypassEnabled: false`; a wrong code is rejected with bypass off (control case); toggling
  bypass on via the admin endpoint persists and is reflected on a subsequent `GET`; the
  same wrong code then succeeds and returns a real token pair; toggling back off is what
  the dev DB was left in.

### Two real bugs found and fixed this session (worth knowing about, won't recur but the pattern might)

1. **JWT claim remapping**: `JwtBearerHandler` silently renames the short `"role"` claim
   to the long `ClaimTypes.Role` URI unless `options.MapInboundClaims = false` is set —
   every authorization policy was failing with an empty 403 until this was found by
   actually running the flow, not by reading the code. Already fixed in `ApiSetup.cs`.
2. **Flutter `compileSdk`**: all three apps were stuck on Flutter's bundled default (36)
   but `flutter_secure_storage` needs 37. Nobody had ever built an APK before this session
   (CI's Flutter jobs never ran until Epic 2 touched `shared/palengke_core` for the first
   time — before that, every push only touched `.NET`/YAML files). Fixed by pinning
   `compileSdk = 37` explicitly in all three `android/app/build.gradle.kts`.

## Epic 3 — done (see the "Epic 3 — closed" section below for the full task list)

An admin can create a market, draw its service polygon and save it; a customer address
inside it is accepted and one outside is refused with a clear message. Proven end-to-end
in one continuous run, not just piece by piece — see task #33 below.

## Epic 4 — Onboarding, KYC & Partner Storefront — in progress

Full task breakdown and the detailed design for tasks #34/#35 live in
`C:\Users\Wilson\.claude\plans\nested-herding-swing.md` — read that file first in a fresh
session before picking up #36. Summary of what a scouting pass found before any code was
written, still true and worth knowing:

- **`partners`/`riders` SQL tables already existed** (migration 001, `status` CHECK
  `pending_kyc/under_review/verified/suspended/rejected`, matching `PartnerStatus`/
  `RiderStatus` C# enums already in `Domain/Identity/Enums.cs`), but **no C# domain
  entities, repositories, services, or endpoints existed for Partner/Rider at all** before
  this epic — OTP verify (`OtpAuthService.VerifyOtpAsync`) only ever inserted a bare
  `users` row. Registering the `partners`/`riders` profile row is in-scope for this epic.
- **`MediaAsset`/`MediaPurpose` already anticipated this epic**: `MediaPurpose.KycDocument`
  and `MediaPurpose.PartnerProduct` already existed with real allowlist policy in
  `Domain/Media/MediaPurposePolicy.cs`, and the generic presign→PUT→commit flow already
  worked end-to-end. `IFileStorage.CreateReadUrl` already exists and is the documented
  mechanism for admin viewing of private KYC documents — no new storage-layer work needed
  for that.
- **Flutter side**: `partner/lib/src/` and `rider/lib/src/` are still Epic-0 placeholder
  shells. `shared/palengke_core`'s `UploadService.uploadImage`/`uploadBytes` already does
  the full presign→PUT→commit round trip generically — KYC upload screens can call it
  directly. `customer/lib/src/eligibility/` (api/dtos/page split) is the pattern to mirror.
- **No `IHostedService`/`BackgroundService` existed anywhere in the codebase.** The daily
  KYC-expiry job (task #39) will be the first background job in the project.

### Task list (dependency order, numbering continues from Epic 3's #33)

1. **#34 Domain entities & enums** ✅ done, committed (`29be416`). `Partner`, `Rider`
   (`Domain/Identity/`), `DocumentType`, `KycDocument`, `KycDocumentStatus`
   (`Domain/Kyc/`), `PartnerProduct` (`Domain/Media/`), `ContentReport`,
   `ContentTargetType`, `ContentReportStatus` (`Domain/Moderation/`). **Deliberately plain
   data** — required init-only properties, no domain methods/guards — matching
   `Category`/`Item`/`Market`'s established convention (confirmed by reading them first)
   rather than `MediaAsset`'s one-off guarded-mutation style. All transition logic (status
   guards, the "latest submission per owner+type" resubmission rule) is Application-layer
   scope, task #36, not yet written.
2. **#35 Migration 004** ✅ done, committed (`44b0856`), applied to the local DB (native
   MySQL, not Docker) and verified. `admin/src/OnlinePalengke.Migrations/Scripts/004_kyc_and_storefront.sql`
   — `document_types`, `kyc_documents`, `partner_products`, `content_reports`, following
   every convention in `README.Database.md` §4 (VARCHAR + named CHECK, not ENUM;
   `BIGINT UNSIGNED`; `DATETIME(6)` written explicitly, no `ON UPDATE CURRENT_TIMESTAMP`).
   Seeds the six role-wide `document_types` rows from the plan (valid ID, business permit,
   barangay clearance for partners; driver's licence, OR/CR, NBI clearance for riders).
   **The two category-scoped rows (sanitary permit, health card for Meat/Poultry/Fish)
   correctly inserted zero rows** on the current local DB — confirmed deliberately: Epic
   3's own verification sessions always cleaned up their test categories afterward, so
   `categories` is empty right now. The migration's `INSERT ... SELECT ... WHERE name IN
   (...)` is written to no-op rather than hard-fail when that happens; re-run is safe once
   real categories exist. Verified: all four tables created, 6/6 seed rows present, a
   second run of the migration runner is a correct no-op, full solution (`dotnet build
   OnlinePalengke.slnx`) builds with 0 warnings/errors, all 53 existing unit tests still
   pass.
3. **#36 Application layer** ✅ done, committed (`d06af00`). Six repository interfaces
   (`IPartnerRepository`, `IRiderRepository`, `IDocumentTypeRepository`,
   `IKycDocumentRepository`, `IPartnerProductRepository`, `IContentReportRepository`) and
   five services: `PartnerService`/`RiderService` (`Application/Onboarding/`),
   `DocumentTypeService`/`KycDocumentService` (`Application/Kyc/`),
   `PartnerProductService` (`Application/Storefront/`), `ContentModerationService`
   (`Application/Moderation/`). Notable design points:
   - `Partner`/`Rider` carry no domain methods (confirmed by reading #34's actual output,
     not a plan sketch — they're plain data like `Category`/`Item`/`Market`), so every
     status change is a full-row reconstruction (`new Partner { ... }`) followed by a
     repository `UpdateAsync`, never a mutation.
   - **Resubmission-preserves-history is real**: `KycDocumentService.SubmitAsync` always
     inserts a new `KycDocument` row rather than touching a rejected one, so "the current
     state of a requirement" is always "the latest row for that owner+document-type",
     recomputed fresh, never tracked as running state anywhere.
   - **The verified-derivation rule** lives in `KycDocumentService.EvaluateVerificationAsync`,
     called after every approval: pulls every `IsRequired` document type for the owner's
     role (and, for a partner, their `CategoryId`) via `IDocumentTypeRepository.ListForRoleAsync`,
     checks each has an `Approved` + unexpired latest submission, and only then promotes to
     `Verified` — it never demotes; a lapse is `KycDocumentService.ExpireAsync`'s job
     (built now, ready for task #39's background job to call, not left as a stub).
   - First submission promotes `PendingKyc → UnderReview`
     (`PartnerService.MarkUnderReviewIfPendingAsync`), but a later resubmission on an
     already-verified owner does not disturb their status — the method is named
     "IfPending" specifically because this distinction matters and is easy to get wrong.
   - `KycDocument.OwnerId` is the **partner/rider profile id**, not the `users.id` — every
     self-service method resolves "my profile" via `IPartnerRepository.GetByUserIdAsync(currentUser.UserId)`
     first, matching how `ICurrentUser` exposes no partner/rider id directly.
   - `PartnerProductService` publishes on save (decision 18, no pre-approval);
     `ContentModerationService.TakeDownAsync` unpublishes through `PartnerProductService`'s
     own path rather than a separate deletion mechanism.

   **Pure Application layer — cannot be exercised end to end yet** (no Infrastructure
   implementations for the six new repositories, task #37; no Api endpoints, task #38).
   Verified by build only, both delegated to a subagent per this session's working
   pattern: `OnlinePalengke.Application` builds standalone (references only `Domain`), the
   full solution builds clean, all 53 existing unit tests still pass. A second subagent
   pass cross-checked every new file against `CategoryService`/`ItemService`/`MarketService`'s
   established conventions (exception constructors, the `ParseStatus`-style enum-parsing
   idiom, `Naming.ToDbValue` usage, the `IsForeignKeyViolation` reuse, and a DI-cycle check
   across all five new services) — no inconsistencies found.
4. **#37 Infrastructure** — not started. Dapper repos for #36's abstractions.
5. **#38 Api endpoints** — not started. Registration, document-types CRUD, KYC submit/
   review/approve/reject, partner-products CRUD+publish, content moderation.
6. **#39 Daily background job** — not started. First `BackgroundService` in the project:
   KYC expiry warnings (30/7 days) and suspension on lapse.
7. **#40 Admin UI** — not started. `DocumentTypes.razor`, wire `Verification.razor` to
   real data, a content-moderation page.
8. **#41 Partner Flutter app** — not started. Registration, KYC document screens, product
   declaration screen.
9. **#42 Rider Flutter app** — not started. Registration, KYC document screens (factor a
   shared widget into `palengke_core` if #41 and #42 turn out near-identical).
10. **#43 End-to-end verification** — not started. The literal exit criterion: a stall
    registers, submits KYC, is rejected, resubmits, is approved, `verified` fires.

The `.col-2` CSS grid layout bug flagged under Epic 3's #30 is still open (unrelated,
doesn't block Epic 4, but worth a look if Epic 4 touches `MarketDetail.razor`-adjacent
layout patterns).

### Epic 3 scope (from the plan)

> Catalog, Markets, Geofencing & the Admin↔API Integration Pattern. This epic is doing
> double duty: it's the first time `OnlinePalengke.Admin` gets any connection to the
> backend at all (today it has zero project references and zero HTTP calls — pure UI
> shell over static `PlaceholderData`), so it's also where the **Admin-to-API integration
> pattern gets designed once**, reused by every later admin epic (4, 8).
>
> Exit criteria: an admin can create a market, draw its service polygon, and a customer
> address inside it is accepted while one outside is refused with a clear message —
> against the real API, not placeholder data.

### Architecture decision made in an earlier session, not yet written into the plan file

**Admin talks to the API over HTTP, like the three Flutter apps do — it does NOT get a
direct C# project reference to `OnlinePalengke.Application`/`Infrastructure`.** The plan
document's repository-layout comment is explicit that there is "the only API, serves all
4 clients" — Blazor Admin is client #4, not a second backend. Admin may reasonably
reference `OnlinePalengke.Domain` directly for shared pure enums/types (no I/O), but all
actual reads/writes go through a typed HTTP client (`AdminApiClient`, task #29) calling
the same `OnlinePalengke.Api` project, mirroring `palengke_core`'s `ApiClient` pattern
(base URL, JWT attachment, typed error handling from ProblemDetails). This is the
integration pattern task #29 needs to build once and well.

### Task list for the rest of Epic 3 (in dependency order — do them in this order)

Task numbers are stable references used across sessions (not a live `TaskList` — nothing
persists there between sessions, this file is the source of truth):

1. **#24 Domain entities** ✅ done, committed (`b211fe1`, `cdb4c67`). `Category`, `Unit`,
   `Item`, `ItemUnit`, `Market`, `MarketStatus`, `DeliveryWindow`, plus
   `Domain/Markets/PolygonWkt.cs` (`ff43cd3`) — a pure GeoJSON↔WKT converter with its own
   unit tests, needed because of the axis-order landmine found in step 2.
2. **#25 Migration 002** ✅ done, committed (`966d10a`), applied to the local DB, smoke-
   tested and rolled back. `categories, units, items, item_units, markets,
   delivery_windows` tables, plus the deferred `partners.market_id`/`category_id` FKs.
   **Two real landmines found and documented in the script's header — read them before
   touching geometry again:**
   - `SPATIAL INDEX` requires every indexed column `NOT NULL`. That's incompatible with
     the real admin flow (market created before its polygon is drawn), so
     `markets.service_area` is `POLYGON NULL` with **no spatial index** — a full-table
     `ST_Contains` scan is fine at wet-market scale (dozens of rows, not thousands).
   - MySQL 8's SRID 4326 WKT axis order is **latitude, longitude** — the reverse of
     GeoJSON's longitude, latitude. Confirmed directly against the server
     (`ST_GeomFromText('POINT(14.667 121.0)', 4326)` is Manila; the swapped order throws
     an out-of-range error). `PolygonWkt` (see #24) isolates this flip to two lines.
3. **#26 Application layer** ✅ done, committed (`fa48520`). `ICategoryRepository`,
   `IUnitRepository`, `IItemRepository`, `IItemUnitRepository`, `IMarketRepository`,
   `IDeliveryWindowRepository` abstractions; `CategoryService`, `UnitService`,
   `ItemService` (Catalog namespace) and `MarketService`, `DeliveryWindowService`,
   `MarketEligibilityService` (Markets namespace); DTOs in `CatalogContracts.cs` /
   `MarketContracts.cs`; all registered in `DependencyInjection.cs`. Notable design
   points: `ItemService` replaces an item's unit set as a whole (delete-then-reinsert in
   one `IUnitOfWork` transaction) to keep "at most one default unit" trivially true;
   `MarketService.UpdateServiceAreaAsync` is separate from the rest of the market's
   fields, matching the admin UI's dedicated "Save area" action; delete on
   Category/Unit/Market catches a real MySQL FK violation (confirmed empirically:
   `DbException.SqlState == "23000"` via MySqlConnector) and turns it into a friendly
   `ConflictException` instead of a raw 500.
4. **#27 Infrastructure** ✅ done, committed (`7d2eb2d`). Dapper repos for all six
   abstractions above, following `MediaAssetRepository`'s established shape. **Verified
   end-to-end against the real local database** (insert/read/update/delete through every
   repo, not just a build check) — 16/16 checks passed. Found and fixed one more
   landmine along the way, this one in the driver itself, not the schema: **reading a
   MySQL `TIME` column straight into a C# `TimeOnly` property does not throw — it
   silently returns midnight regardless of the stored value**, with this
   Dapper 2.1.79 / MySqlConnector 2.6.2 pair. `DeliveryWindowRepository` routes
   `starts_at`/`ends_at` through `TimeSpan` on both read and write, converting to/from
   `TimeOnly` explicitly in the row-mapping code — that round-trips correctly. (The
   verification harness itself briefly produced 4 false failures from forgetting to call
   `DapperConfiguration.Apply()` — a good reminder that a standalone check needs the same
   startup wiring as the real app, not evidence of an app bug.)
5. **#28 Api endpoints** ✅ done, committed (`5f55358`). `CatalogEndpoints.cs`
   (categories/units/items admin CRUD) and `MarketEndpoints.cs` (markets admin CRUD +
   `PUT /{id}/service-area` + delivery-windows admin CRUD, plus the customer-facing
   `POST /api/customer/markets/eligibility`), wired into `ApiSetup.MapApiGroups`. Follows
   the existing minimal-API style (`AuthEndpoints.cs`/`UploadEndpoints.cs`) — `Results.Ok`
   everywhere including POST, `AppException` subclasses already map to ProblemDetails via
   `GlobalExceptionHandler`, no new pattern invented. **Verified end-to-end against the
   real local database and real JWT auth**, not just a build check: logged in as the test
   admin, created a category/unit/item, created a market, saved a real GeoJSON service
   polygon through `PUT /service-area`, activated the market (blocked correctly until the
   polygon existed), added a delivery window (confirmed the `TimeOnly` values round-trip
   correctly through the API, not just the repo layer), then logged in as a test customer
   via real OTP and hit `/api/customer/markets/eligibility` with a point inside the
   polygon (accepted, market returned) and a point outside it (refused with the clear
   message) — this is the literal Epic 3 exit criterion, passing against the real stack.
   All test data cleaned up afterward via the same DELETE endpoints.
6. **#29 AdminApiClient + admin login + Blazor auth-state** ✅ done, committed
   (`2104d86`). `Api/AdminApiClient.cs` (typed HttpClient — bearer attachment, typed
   `AdminApiException` from ProblemDetails, one silent refresh-and-retry on 401, mirroring
   `palengke_core`'s `ApiClient`), `Auth/AdminAuthenticationStateProvider.cs` (holds the
   JWT session for the circuit, persists to `ProtectedSessionStorage` so it survives a
   reload), `Components/Pages/Login.razor` (Admin's first real auth UI — was zero before
   this). Every other page now carries `@attribute [Authorize]`, enforced by
   `AuthorizeRouteView` in `Routes.razor`.

   **Two real bugs found only by driving the app in an actual headless-Chromium browser**
   (Playwright — Blazor Server's interactive SignalR circuit can't be exercised with
   curl, a plain GET can't see what the client-side router does):
   - `AuthorizationOptions.FallbackPolicy` makes ASP.NET Core auto-insert the
     `UseAuthorization()` HTTP middleware, which tries to `Challenge()` *every*
     unattributed endpoint — including the SignalR circuit endpoint itself — and crashes
     with no auth scheme registered to challenge with.
   - Razor Components routing projects each page's own `[Authorize]` attribute onto that
     route's HTTP endpoint metadata. Even with a scheme wired up so the HTTP-level
     challenge could redirect instead of crashing, that same check fired on *every*
     full-page load — including a reload of an already-signed-in tab — bouncing the admin
     back to `/login` before the circuit ever reconnected and read the persisted session,
     defeating the entire point of persisting it. Fixed by chaining `.AllowAnonymous()`
     onto `MapRazorComponents` (`AllowAnonymous` metadata always wins over `Authorize` on
     the same endpoint) — this keeps the HTTP layer out of the decision entirely; real
     enforcement is `AuthorizeRouteView`, evaluated only once inside the circuit, after
     `AdminAuthenticationStateProvider` has had the chance to hydrate from storage.

   **Verified end-to-end in a real headless-Chromium session** against the live API and
   DB (Playwright installed ad hoc into a scratch temp dir, not added to the repo):
   unauthenticated `/` redirects to `/login`; wrong credentials show an inline error and
   leave the form usable; correct credentials land on the dashboard with the admin's
   email and a Sign out button in the top bar; **a full page reload while signed in stays
   signed in** (the actual deliverable this task exists for); Sign out returns to
   `/login` and a subsequent visit is redirected again, confirming the session was really
   cleared, not just hidden client-side.
7. **#30 Vendor Leaflet + implement the map JS interop** ✅ done, committed (`ed0d099`).
   Leaflet 1.9.4 and Leaflet.draw 1.0.4 vendored into `wwwroot/lib/leaflet/` (fetched from
   jsdelivr, matching the README's pinned versions — not a CDN reference at runtime), the
   four `LEAFLET SEAM` tags in `App.razor` uncommented. `ServiceAreaMap.razor` is a real
   editor now: `wwwroot/js/service-area-map.js` does `init`/`load`/`destroy`, one Leaflet
   map instance per element id, at most one polygon at a time (matches a market having a
   single service area — a new draw or a finished edit replaces whatever was there), edits
   flow back to Blazor via `[JSInvokable] OnPolygonEditedFromJs`. Parameter contract
   (`MarketId`, `InitialPolygonGeoJson`, `OnPolygonChanged`, `ReadOnly`) is unchanged from
   the seam, as intended. Tile source is still public OSM tiles (zero-setup default,
   unchanged from before — revisit before real production traffic, per the README).

   **Verified in both headless and headed real Chromium** against the live app: a
   market's saved polygon loads and the view fits its bounds, the draw toolbar draws a new
   polygon on a market with none, edits round-trip back through the `[JSInvokable]`
   callback correctly, no console errors.

   **Found but did NOT fix — flagged for a dedicated follow-up, read before touching
   `admin.css` layout again:** `.col-2`/`.col-2--wide-first` (the two-column grid used by
   `MarketDetail.razor` and the `Dashboard`) collapses to ~180px wide regardless of
   viewport size, when rendered through the live Blazor Server circuit — confirmed
   unrelated to this session's work (reproduces on the untouched Dashboard too) and
   confirmed *not* a CSS authoring bug (the exact same markup + `admin.css`, served
   statically with no Blazor involved at all, lays out correctly at full width). Also not
   a headless-only artifact — reproduces in a real headed Chromium window too. A
   `resize` event does not fix it. Root cause not found; something about how the live
   circuit renders this specific grid differs from a plain static page load. This
   materially affects usability of `.col-2` pages today (the working map from this task is
   visually squeezed into a sliver on `MarketDetail.razor`) — worth its own investigation
   before or during #31, since #31 touches these same pages anyway.
8. **#31 Wire the Razor pages to real data** ✅ done, committed (`34ecef3`).
   `Categories.razor`, `Units.razor`, `Items.razor`, `MarketList.razor` and
   `MarketDetail.razor` (map, delivery windows, market edit) all call `AdminApiClient`
   now, with full create/edit/delete — the plan's Epic 3 deliverable is explicitly "Admin
   CRUD", not read-only lists, so disabled buttons were built out rather than left inert.
   `Api/CatalogDtos.cs` / `Api/MarketDtos.cs` are Admin's own wire-DTO copies (same
   no-Application-reference pattern as `Api/AdminApiDtos.cs` from #29).
   `AdminViewModels.cs`'s `CategoryRow`/`ItemRow`/`UnitRow`/`MarketRow`/
   `DeliveryWindowRow` ids are now `long` (decided: widen, not narrow, matching every id
   in the real API). `PlaceholderData.cs` itself is untouched — `DeliveryRuns.razor`
   still reads `PlaceholderData.Markets` and is out of this task's scope.

   **Two scope calls made and worth knowing about:** item image upload is out (create
   always sends a null `MediaAssetId`, edit preserves whatever the item already had —
   real S3 presign/upload wiring is separable work); "Verified stalls" was removed from
   the Market screens rather than wired to a fake `0`, since no partner/stall
   verification system exists yet.

   **Verified end-to-end in a real Chromium browser** against the live API and DB,
   covering the literal Epic 3 exit criterion through the actual UI (not just direct API
   calls, unlike #28's verification): created a category/unit/item, created a market,
   drew a real polygon with Leaflet.draw and saved it, edited the market to Active
   (correctly rejected until the area was saved), added/edited/removed a delivery
   window, then — via the real customer-facing API — confirmed a point inside the
   admin-drawn polygon is accepted and a point outside is refused with a clear message.
   Two apparent failures during verification turned out to be testing-technique bugs,
   not app bugs, confirmed by re-running the same steps in isolation: (1) closing a
   Leaflet.draw polygon by re-clicking the first vertex's *page coordinate* is not
   reliable automation — use the toolbar's `a[title="Finish drawing"]` link instead; (2)
   a combined multi-step script had a timing race clicking through modals back-to-back.
   No application code changed for either.
9. **#32 Customer app address + eligibility** ✅ done, committed (`e7c31af`).
   `EligibilityPage` (`customer/lib/src/eligibility/`) calls the real
   `POST /api/customer/markets/eligibility` endpoint via a new `EligibilityApi` +
   `EligibilityDtos` pair kept local to the customer app (same no-shared-package
   reasoning as Admin's own DTOs — no other app calls this endpoint). Reachable from
   `HomePage`'s new "Check delivery eligibility" button. **Decided: latitude/longitude
   entry, not a `flutter_map` pin-picker** — the plan explicitly allowed a simpler form
   for v1, and a real map dependency + permissions + gesture handling is separable work;
   revisit if/when the customer app gets a proper address-entry flow. Fields default to
   Manila's coordinates so the form is never blank.

   **Verified against the real running API**, not just `flutter analyze` (which is
   clean) — Flutter's default web renderer draws to one canvas with no real DOM, so the
   Playwright-style automation that verified the Blazor/Leaflet side of Epic 3 doesn't
   apply here. Verification instead drove the real `AuthApi`/`SessionManager`/
   `ApiClient` code path directly (a throwaway `flutter_test` file, deleted before
   committing — not a permanent test): a real customer OTP login, then two live
   eligibility calls — a point inside an admin-drawn, *active* market's polygon
   correctly decoded `isEligible: true` with a populated `markets` list, and a point far
   away decoded `isEligible: false` with the same refusal message #28 verified
   server-side. Both prove the DTOs' JSON field names actually match the live API, not
   just what the C# source says they should be. (One thing this caught along the way,
   unrelated to the app code: `MarketEligibilityService` only matches *active* markets —
   an `onboarding` market with a saved polygon still correctly reports no coverage.)
10. **#33 End-to-end verification** ✅ done. Ran the exit criteria as one continuous
    session against the real local database, no reset in between the two halves (unlike
    #31/#32, which each verified their own half separately against test rows cleaned up
    before the other's session started): Playwright drove the real Blazor admin UI to
    create "Exit Criteria Market", draw a real polygon with Leaflet.draw centered on
    (14.5995, 120.9842), save it, and activate the market — then, immediately after, in
    the same live database state, a real customer OTP-logged in through the actual
    `AuthApi`/`SessionManager`/`ApiClient`/`EligibilityApi` production code path (the
    exact code `EligibilityPage` calls) and checked eligibility: the market's own
    coordinates correctly came back `isEligible: true` with `Exit Criteria Market` in the
    returned list, and a point in Cebu (~570 km away) correctly came back `false` with
    the refusal message. Both admin and customer test rows were deleted immediately
    after. **This is the Epic 3 exit criterion, proven, closing the epic**: an admin can
    create a market, draw its service polygon, and a customer address inside it is
    accepted while one outside is refused with a clear message.

## Epic 3 — closed

All ten tasks (#24-#33) are done, committed, pushed, and each verified against the real
local database — most of them in a real browser or through the real production client
code path, not just by reading the code. The `.col-2` CSS grid layout bug flagged under
#30 remains open (unrelated to Epic 3's own scope, still affects `MarketDetail.razor` and
the `Dashboard`) — worth a dedicated look whenever those pages are next touched. Item
image upload (flagged under #31) and a `flutter_map` pin-picker for the customer address
screen (flagged under #32) are both deliberately-scoped-out pieces of future work, not
gaps in what Epic 3 asked for.

## Working patterns established this session (keep following these)

- **Granular commits.** One logical change per commit, pushed immediately after local
  verification. Never batch multiple unrelated changes into one commit.
- **Verify against the real thing, not just "should work."** Epic 2's JWT claim bug and
  the compileSdk bug were both invisible from code review alone and only surfaced by
  actually running the flow / actually building an APK. Epic 3's Infrastructure layer
  reinforced this hard: the MySQL SRID 4326 axis order and the `SPATIAL INDEX` NOT NULL
  requirement (found writing migration 002) and the silent `TimeOnly` read bug (found
  writing `DeliveryWindowRepository`) were all invisible from code review and would have
  shipped as confident-looking, wrong code. Keep budgeting real-DB verification time for
  the rest of Epic 3 — actually draw a polygon in the browser, actually save it, actually
  hit the eligibility endpoint with a real point.
- **Least privilege on every credential.** The MySQL `palengke` user is scoped to one
  database even though root was available. Apply the same instinct to anything Epic 3
  touches.
- **Read the seam docs before writing code.** This codebase has unusually good
  forward-planning comments (the Leaflet README, the migration script's deferred-FK
  note) — they exist specifically so a future session doesn't have to rediscover context.
- **Don't invent scope beyond what's asked.** When the user scoped Epic 2 down to
  "OTP for now, no actual SMS," that stub was built cleanly with a clear swap-out seam,
  not half-heartedly. Apply the same discipline to any scope calls in Epic 3.
