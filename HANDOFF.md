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
- **Epic 2 (Real Identity & Auth)**: phone-OTP + JWT fully implemented and verified live
  end-to-end (request → verify → authenticated call → refresh → rotate → replay-rejected
  → signout → idempotent-signout → cross-role-conflict-rejected). Admin email+password
  login also implemented and verified (correct login, wrong password, nonexistent email
  all behave correctly, no user-enumeration). `DevHeaderAuthenticationHandler` is **still
  present and still active** — Epic 2's plan said retiring it is optional until Epic 9,
  and it now coexists with real JWT auth via a "Smart" policy scheme in `ApiSetup.cs` that
  forwards to JWT when a Bearer header is present, DevHeader otherwise.

  SMS sending is **stubbed** — `LoggingSmsSender` in Infrastructure logs the OTP code
  instead of sending a real SMS (this was explicit, requested scope: "Provision OTP for
  now... no actual OTP sent"). Swap the one DI registration in
  `OnlinePalengke.Infrastructure/DependencyInjection.cs` for a real Semaphore/Movider
  adapter later; nothing above `ISmsSender` needs to change.

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

**Uncommitted, mid-flight.** Four Domain files exist on disk, not yet committed:
`admin/src/OnlinePalengke.Domain/Catalog/{Category,Unit,Item,ItemUnit}.cs`. That's
task #24 (below) roughly half-done — `Market` and `DeliveryWindow` entities not written
yet.

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

### Architecture decision made this session, not yet written into the plan file

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

Task numbers are from this session's `TaskList` (`TaskGet`/`TaskList` to see live status):

1. **#24 Domain entities** — `Category`✅ `Unit`✅ `Item`✅ `ItemUnit`✅ done.
   `Market` and `DeliveryWindow` **not yet written**. Market needs a polygon
   representation — plan says WKT crosses the Dapper boundary
   (`ST_AsText`/`ST_GeomFromText`), so the Domain entity itself can hold the WKT string
   or a simple `IReadOnlyList<(double Lat, double Lng)>` ring — pick one and keep it
   consistent with how `MediaAsset`/`OtpCode` etc. are written elsewhere in this codebase
   (plain properties, `required init`, XML remarks explaining the *why* not the *what*).
   Also add a `MarketStatus` enum — PlaceholderData shows three real states (Onboarding,
   Active, Suspended), not just a boolean.
2. **#25 Migration 002** — `categories, items, item_units, markets, delivery_windows`
   tables, **plus backfill the two FKs on `partners`** that script 001 deliberately left
   dangling (`partners.market_id`, `partners.category_id` — see the `NOTE:` comment in
   `001_identity_and_media.sql` right above `CREATE TABLE partners`). `markets` needs
   `SPATIAL INDEX` on `service_area POLYGON NOT NULL SRID 4326`. Decided this session:
   `markets` also gets explicit `city`/`province` columns (not just a free-text
   `address`) and `categories` gets a `slug` column — both needed because
   `AdminViewModels.cs`'s existing `MarketRow`/`CategoryRow` already display them; the
   plan's Data model section was a sketch, not exhaustive DDL, fleshing it out here is
   expected. `item_units` needs an `is_default` boolean (one item can have multiple valid
   units — kg or piece — with one marked default for shopping-list UI).
3. **#26 Application layer** — repos + services + DTOs for catalog and markets, plus an
   eligibility use case: given lat/lng, which markets' polygons contain the point.
4. **#27 Infrastructure** — Dapper repos, WKT round-trip, `ST_Contains` query.
5. **#28 Api endpoints** — admin CRUD under `/api/admin/*` for all five entities, plus a
   customer-facing eligibility endpoint (lat/lng in, eligible markets out).
6. **#29 AdminApiClient + admin login + Blazor auth-state** — the reusable pattern
   described above. Needs a real login page (none exists yet — Admin currently has zero
   auth UI) and something implementing Blazor Server's `AuthenticationStateProvider` so a
   signed-in session survives across the SignalR circuit. Calls
   `POST /api/admin/auth/login` (already built and verified in Epic 2).
7. **#30 Vendor Leaflet + implement the map JS interop** — **this seam is unusually
   well-prepared, read it before doing anything else in this task**:
   `admin/src/OnlinePalengke.Admin/wwwroot/lib/leaflet/README.md` gives exact pinned
   versions (Leaflet 1.9.x, Leaflet.draw 1.0.4), exact files to vendor, and a numbered
   wiring checklist. `Components/App.razor` has four commented-out tags marked
   `LEAFLET SEAM` ready to uncomment. `Components/Markets/ServiceAreaMap.razor` already
   declares the full parameter contract (`MarketId`, `InitialPolygonGeoJson`,
   `OnPolygonChanged`, an `ElementReference` host) — implementing it is adding
   `wwwroot/js/service-area-map.js` (`init`/`load`/`destroy`) and JS interop glue, not
   redesigning anything. Confirmed this session: outbound internet access works fine for
   `curl`ing the library files from jsdelivr to vendor them locally (the README's "no CDN
   at runtime" constraint is about the deployed app fetching at request time, not about
   this one-time download-and-commit step). Tile source undecided — public OSM tiles are
   the zero-setup default for now; the README flags this needs revisiting before real
   production traffic (OSM's usage policy disallows heavy production load on the public
   servers).
8. **#31 Wire the Razor pages to real data** — `Categories.razor`, `Items.razor`,
   `Units.razor`, `MarketList.razor`, `MarketDetail.razor` (including delivery windows)
   swap `PlaceholderData.X` for `AdminApiClient` calls. Keep `AdminViewModels.cs`'s
   existing record shapes as the binding model where reasonable (the Razor markup already
   binds their exact property names) — **but note their `Id` properties are `int`; every
   other id in this codebase is `long` (`BIGINT UNSIGNED`), so either widen these to
   `long` or make sure the DTOs narrow consistently. Decide once, do it everywhere.**
9. **#32 Customer app address + eligibility** — lower priority than the admin-side work
   for satisfying the exit criteria (the exit criteria's polygon-drawing half is entirely
   admin-side). A real screen calling the eligibility endpoint is required; whether it's
   a full interactive map-pin picker (`flutter_map` — the Flutter/OSM equivalent of
   Leaflet, no API key needed, consistent with avoiding Google Maps billing setup — was
   the leaning, not yet decided/started) or a simpler working form is still open. Don't
   let this block finishing the admin side first.
10. **#33 End-to-end verification** — the actual exit criteria scenario: create a market
    in admin, draw a real polygon, save it, then hit the eligibility endpoint (or the
    customer app) with a point inside and a point outside and confirm accept/reject with
    a clear message. Do this against the real local database, same pattern as Epic 2's
    curl-based verification walkthrough.

## Working patterns established this session (keep following these)

- **Granular commits.** One logical change per commit, pushed immediately after local
  verification. Never batch multiple unrelated changes into one commit.
- **Verify against the real thing, not just "should work."** Epic 2's JWT claim bug and
  the compileSdk bug were both invisible from code review alone and only surfaced by
  actually running the flow / actually building an APK. Budget time for this on Epic 3
  too — actually draw a polygon, actually save it, actually query `ST_Contains`.
- **Least privilege on every credential.** The MySQL `palengke` user is scoped to one
  database even though root was available. Apply the same instinct to anything Epic 3
  touches.
- **Read the seam docs before writing code.** This codebase has unusually good
  forward-planning comments (the Leaflet README, the migration script's deferred-FK
  note) — they exist specifically so a future session doesn't have to rediscover context.
- **Don't invent scope beyond what's asked.** When the user scoped Epic 2 down to
  "OTP for now, no actual SMS," that stub was built cleanly with a clear swap-out seam,
  not half-heartedly. Apply the same discipline to any scope calls in Epic 3.
