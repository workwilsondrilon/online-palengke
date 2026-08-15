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

## What's in progress right now — Epic 3

**Backend is fully built and reachable over HTTP now — Domain through Api, committed and
pushed, verified against the real local database and real JWT auth.** Tasks #24-#28
(below) are done. What's left is the Admin-to-API integration pattern, Leaflet, and the
customer app — i.e. everything that makes the built-and-reachable backend visible in an
actual UI.

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
6. **#29 AdminApiClient + admin login + Blazor auth-state — not started.** The reusable
   pattern described above. Needs a real login page (none exists yet — Admin currently
   has zero auth UI) and something implementing Blazor Server's
   `AuthenticationStateProvider` so a signed-in session survives across the SignalR
   circuit. Calls `POST /api/admin/auth/login` (already built and verified in Epic 2).
   This is the task HANDOFF from the prior session called out as needing focused
   attention — treat it as its own deliverable, not something to rush alongside #28.
7. **#30 Vendor Leaflet + implement the map JS interop — not started.** **This seam is
   unusually well-prepared, read it before doing anything else in this task**:
   `admin/src/OnlinePalengke.Admin/wwwroot/lib/leaflet/README.md` gives exact pinned
   versions (Leaflet 1.9.x, Leaflet.draw 1.0.4), exact files to vendor, and a numbered
   wiring checklist. `Components/App.razor` has four commented-out tags marked
   `LEAFLET SEAM` ready to uncomment. `Components/Markets/ServiceAreaMap.razor` already
   declares the full parameter contract (`MarketId`, `InitialPolygonGeoJson`,
   `OnPolygonChanged`, an `ElementReference` host) — implementing it is adding
   `wwwroot/js/service-area-map.js` (`init`/`load`/`destroy`) and JS interop glue, not
   redesigning anything. The GeoJSON this map produces/consumes is exactly what
   `UpdateServiceAreaRequest.PolygonGeoJson` expects — no conversion needed on the Admin
   side, `MarketService` does the WKT flip server-side. Confirmed in an earlier session:
   outbound internet access works fine for `curl`ing the library files from jsdelivr to
   vendor them locally. Tile source undecided — public OSM tiles are the zero-setup
   default for now; the README flags this needs revisiting before real production
   traffic.
8. **#31 Wire the Razor pages to real data — not started.** `Categories.razor`,
   `Items.razor`, `Units.razor`, `MarketList.razor`, `MarketDetail.razor` (including
   delivery windows) swap `PlaceholderData.X` for `AdminApiClient` calls. Keep
   `AdminViewModels.cs`'s existing record shapes as the binding model where reasonable
   (the Razor markup already binds their exact property names) — **but note their `Id`
   properties are `int`; every id in the new Application-layer DTOs (`CategoryResponse`,
   `MarketResponse`, etc.) is `long`, matching every other id in this codebase
   (`BIGINT UNSIGNED`). Decide once whether to widen `AdminViewModels` to `long` or narrow
   at the mapping boundary, then do it everywhere — don't mix.**
9. **#32 Customer app address + eligibility** — lower priority than the admin-side work
   for satisfying the exit criteria (the exit criteria's polygon-drawing half is entirely
   admin-side). A real screen calling the eligibility endpoint (`MarketEligibilityService`
   → `#28`'s customer-facing endpoint) is required; whether it's a full interactive
   map-pin picker (`flutter_map` — the Flutter/OSM equivalent of Leaflet, no API key
   needed, consistent with avoiding Google Maps billing setup — was the leaning, not yet
   decided/started) or a simpler working form is still open. Don't let this block
   finishing the admin side first.
10. **#33 End-to-end verification** — the actual exit criteria scenario: create a market
    in admin, draw a real polygon, save it, then hit the eligibility endpoint (or the
    customer app) with a point inside and a point outside and confirm accept/reject with
    a clear message. Do this against the real local database, same pattern used to verify
    #27. The Application/Infrastructure layers underneath this are already proven correct
    (see #27) — this step is specifically about proving the wiring above them (Api →
    Admin/Flutter → user-visible result), not re-proving the geometry.

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
