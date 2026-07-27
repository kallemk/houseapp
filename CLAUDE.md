# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A private app for two people (a couple) to track their house: value over time, a
renovation/investment log, and document/photo storage. Personal-use scale (2 accounts,
ever) — favor simplicity and low/no ongoing Azure cost over enterprise patterns when making
decisions here.

## Commands

### Backend (`backend/`, ASP.NET Core Web API, .NET 9)
```
cd backend
dotnet build                          # build the solution
dotnet test                           # run all xUnit tests
dotnet test --filter FullyQualifiedName~AuthControllerTests   # run a single test class
cd src/HouseApp.Api && dotnet run --launch-profile https      # run locally, https://localhost:7275
```
Swagger UI is available at `/swagger` in Development only.

### Frontend (`frontend/`, Vite + React + TypeScript)
```
cd frontend
npm install
npm run dev        # dev server at http://localhost:5173, proxies /api to the backend (see vite.config.ts)
npm run build      # tsc -b && vite build
npm run lint       # oxlint
```
Node must be 20.19+ or 22.12+ (Vite 8 requirement). On Windows, if `npm install` fails to
pull a native binding (oxlint/rolldown) with an "optional dependencies" error, this is a
known npm bug (npm/cli#4828) — see `optionalDependencies` in `frontend/package.json`,
where the Windows bindings are pinned explicitly as a workaround.

### Infra (`infra/`, Bicep)
```
az bicep build --file infra/main.bicep     # syntax/schema check, no Azure needed
az deployment group what-if --resource-group <rg> --template-file infra/main.bicep --parameters infra/main.parameters.json --parameters seedUser1='{...}' seedUser2='{...}'
```

### Local dev stack
```
docker compose up -d     # Cosmos DB Emulator (localhost:8081) + Azurite (localhost:10000)
```
Run this before `dotnet run` — the API connects to these on startup (Data Protection key
persistence and the DbContext both need them reachable immediately).

## Architecture

**Three independent projects in one repo, no shared code between them**: `backend/`
(.NET), `frontend/` (TS), `infra/` (Bicep). They integrate only over HTTP and Azure
resource wiring, not via shared packages.

### Auth is deliberately not ASP.NET Core Identity

There is no public registration endpoint anywhere in `AuthController`. `Data/Seed/DbSeeder.cs`
creates the **bootstrap** accounts from config (`Seed:Users` / `Seed__Users__N__*`) on every
startup (idempotent — skips if already present), in every environment except `Testing`. This
seeding-on-every-startup behavior is intentional and required for production: App Service has no
separate migration step, so this is the only way the first account can exist. Everyone after that
is added in-app via `UsersController` / `pages/UsersPage.tsx`.

Because the store is Cosmos DB (not relational), full ASP.NET Core Identity was dropped in
favor of a lightweight hand-rolled scheme: `PasswordHasher<ApplicationUser>` for hashing +
plain cookie authentication (`AddAuthentication().AddCookie()`, manual
`HttpContext.SignInAsync`/`SignOutAsync` in `AuthController`). Don't reintroduce
`UserManager`/`SignInManager`/`IdentityDbContext` — they assume a relational store.

**Two ways in, one session.** Password login (`POST /api/auth/login`) and Google
(`POST /api/auth/google`) both end at the same private `AuthController.SignInAsync`, issuing the
same cookie. Everything downstream — `[Authorize]`, Data Protection, the `SameSite=Lax`
same-origin design — is identical regardless of how you signed in.

**`ClaimTypes.NameIdentifier` must always carry `ApplicationUser.Id` — never Google's `sub`.**
That id is stored in `Property.MemberUserIds`, `ValuationEntry.CreatedByUserId`,
`RenovationEntry.CreatedByUserId` and `Document.UploadedByUserId`, and there is no migration
mechanism to rewrite them. Google sign-in therefore **matches an existing user by email
(case-insensitively) and never creates one** — if it minted new rows, both users would silently
lose sight of every existing property.

**The `users` container doubles as the sign-in allowlist.** A Google account is accepted iff a
user row with that email exists; otherwise `/api/auth/google` returns **403** (deliberately not
401 — the frontend distinguishes "not invited" from "sign-in failed"). Deleting a user in the
admin page is therefore how you revoke access.

Google sign-in uses the **ID-token flow**, not a server-side OAuth redirect: the browser gets a
token from Google Identity Services and posts it to the API, which verifies it via
`IGoogleTokenValidator` (`Google.Apis.Auth`). This is why there is **no client secret anywhere**
and no `AddGoogle()` handler. It was chosen specifically because a redirect flow behind the
Static Web App's linked-backend proxy would redirect to the App Service hostname and land the
session cookie on the wrong domain. `IGoogleTokenValidator` is an interface purely so tests can
substitute `FakeGoogleTokenValidator`, exactly as `IBlobStorageService` is handled.

`ApplicationUser.PasswordHash` is **nullable**: users added for Google-only access have none, and
`Login` must reject a null/empty hash rather than feeding it to the hasher.

New users must be **backfilled onto existing properties** (`UsersController.Create` does this).
`PropertiesController.Create` only stamps the users existing *at that moment* into
`MemberUserIds`, so without the backfill an invited person signs in to an empty property list.

Cookie config in `Extensions/ServiceCollectionExtensions.cs` (`AddHouseAppCookieAuth`) uses
`SameSiteMode.Lax` and `CookieSecurePolicy.SameAsRequest` on purpose:
- **Lax + no CORS works** because the frontend and backend are same-origin in both
  environments: the Vite dev proxy forwards `/api` to the backend locally
  (`vite.config.ts`), and the Static Web App's **linked backend** proxies `/api/*` to the
  App Service in production (`infra/modules/staticWebApp.bicep`). If you ever see CORS
  errors, the fix is to preserve this same-origin proxying, not to add a CORS policy.
  Don't use `CookieSecurePolicy.Always`.

### Data layer: EF Core against Cosmos DB, not a relational DB

`Data/AppDbContext.cs` is a plain `DbContext` (not `IdentityDbContext`) using the EF Core
Cosmos provider. Each entity maps to its own container via `ToContainer(...)` +
`HasPartitionKey(...)` in `OnModelCreating`:
- `users`, `properties` — partitioned by `/id`
- `valuationEntries`, `renovationEntries`, `documents` — partitioned by `/propertyId`
  (so "all entries for a property" queries stay single-partition)

There are **no EF Core relational migrations**. Containers/partition keys are provisioned
by `infra/modules/cosmos.bicep` in Azure; locally, `Program.cs` calls
`db.Database.EnsureCreatedAsync()` in Development only. Schema evolution happens in
application code (tolerant reads), not via a migration step.

Controllers that update/delete a single item (`ValuationsController`,
`RenovationEntriesController`, `DocumentsController`) require the `propertyId` (partition
key) as a query-string parameter on `PUT`/`DELETE` — this is required by the frontend API
client (`frontend/src/api/*.ts`), not optional plumbing.

**Cosmos provider footguns that only show up against real Cosmos DB, not the InMemory
provider the tests use** (all three crashed the App Service on startup in production before
being caught — the test suite did not catch any of them):
- `HasIndex(...)` in `OnModelCreating` throws at model-validation time — the Cosmos provider
  doesn't support EF index declarations at all (it indexes every property automatically by
  default). Don't add one.
- Any synchronous EF query (e.g. `db.Users.Any(...)` instead of `AnyAsync`) throws
  `SyncNotSupported` — Cosmos disallows sync I/O outright, unlike relational providers
  which just block a thread. Every DB call must be the `Async` form, no exceptions.
- Predicate-argument aggregate queries — `SingleOrDefaultAsync(predicate)`,
  `AnyAsync(predicate)`, `FirstAsync(predicate)` — generate SQL that Cosmos has rejected
  with `Identifier 'root' could not be resolved` (BadRequest). The fix used throughout this
  codebase is `.Where(predicate).ToListAsync()` followed by the client-side LINQ operator
  (`.SingleOrDefault()`, `.Count > 0`, etc.) instead of passing the predicate straight to the
  terminal async method — see `AuthController.Login` and `DbSeeder.SeedAsync`. Follow this
  same shape for any new single-item-by-predicate lookup; don't reintroduce the inline-
  predicate form.
- Partition-key property casing must match the container's Bicep-declared path exactly.
  Cosmos extracts the partition key value from the document JSON server-side using that path
  (`infra/modules/cosmos.bicep`: `/propertyId`, lowercase); EF Core only special-cases the
  primary `Id`/`id` property to lowercase, so any other property used as a partition key (e.g.
  `PropertyId` on `ValuationEntry`/`RenovationEntry`/`Document`) gets written under its literal
  PascalCase C# name unless told otherwise — every write then fails with `PartitionKeyMismatch`
  (extracted key doesn't match the one in the request header). Fixed via
  `.Property(x => x.PropertyId).ToJsonProperty("propertyId")` in `AppDbContext`. If you add a
  new partition-key property, add the matching `ToJsonProperty` call too.
- `AddHouseAppData`'s `DefaultAzureCredential` is created **once, outside** the `AddDbContext`
  options lambda, not inside it. `AddDbContext` registers `AppDbContext` as Scoped, so that
  lambda re-runs on every request (once per DI scope) — a fresh `DefaultAzureCredential`
  instance each time defeats EF Core's internal service-provider cache (the credential is part
  of the cache key), and after 20 requests this throws `ManyServiceProvidersCreatedWarning`
  (one of the EF Core warnings that's throw-by-default, not just logged). Any other per-request
  service registered via `AddDbContext`'s options lambda needs the same care — construct
  expensive/identity-bearing objects outside the lambda and capture them in the closure.

### No Azure account keys or connection secrets anywhere

Cosmos DB and Storage are both accessed via the App Service's user-assigned managed
identity using RBAC (`disableLocalAuth: true` on the Cosmos account; no storage account
keys). `Extensions/ServiceCollectionExtensions.cs` branches on whether a connection string
is configured (`ConnectionStrings:Cosmos` / `ConnectionStrings:Storage`, set only in
`appsettings.Development.json` for the local emulators) vs. an endpoint URL +
`DefaultAzureCredential` (production, via `Cosmos:AccountEndpoint` / `Storage:AccountUrl`
app settings wired from Bicep outputs). When touching auth/storage wiring, preserve both
branches — don't assume a connection string always exists.

Documents/photos never pass through the API: `DocumentsController.GetUploadUrl` /
`GetDownloadUrl` issue short-lived SAS URLs (`Services/BlobStorageService.cs`), and the
browser PUTs/GETs directly to Blob Storage. `BlobStorageService` transparently supports
both a shared-key SAS (local dev via Azurite connection string,
`BlobClient.CanGenerateSasUri == true`) and a user-delegation SAS (production, managed
identity, no account key) — same code path, branched at runtime.

**Data Protection key persistence is load-bearing, not optional infrastructure**: cookie
auth encrypts the session cookie with the Data Protection key ring. `AddHouseAppDataProtection`
persists it to **local disk** (`PersistKeysToFileSystem`), not Blob Storage — on App Service
Linux this resolves to `/home/data-protection-keys`, which is persistent across
restarts/idle-unloads for a single instance (our F1 plan runs exactly one), so logins survive
them. **Not** encrypted at rest with Key Vault — `ProtectKeysWithAzureKeyVault` needs a URI to
a specific key inside the vault (`.../keys/<name>`), not the vault's own base URI, so wiring
it up properly means provisioning a Key Vault key via Bicep too; skipped as unnecessary
hardening for a 2-user app (this crashed startup with `Invalid ObjectIdentifier ... Bad number
of segments: 1` before being removed). **Key Vault is consequently unused by the app** —
`infra/modules/keyVault.bicep` and the `KeyVault__Uri` app setting are currently dead weight,
kept only because removing the resource is a separate decision from fixing the crash it
caused. Don't switch key persistence back to Blob Storage without reading the git history
first — it was deliberately moved off Blob after `AzureBlobXmlRepository`'s key-ring read
crashed every login with `CryptographicException` → `InvalidQueryParameterValue` (empty
`comp` parameter), traced to the SDK's default download transfer validation being rejected by
this storage account (see the `CreateBlobClientOptions` note below) — moving key persistence
to local disk sidesteps the Blob SDK for this entirely rather than trusting that workaround.

**Every `BlobServiceClient`/`BlobContainerClient` — currently just the one `AddHouseAppBlobStorage`
constructs for documents — is built with download transfer validation disabled**
(`ServiceCollectionExtensions.CreateBlobClientOptions()`, `ChecksumAlgorithm =
StorageChecksumAlgorithm.None`), for the reason above. `BlobStorageService` itself never
downloads blob content (it only issues SAS URLs), so this is defensive for now — but any new
code that reads blob content directly needs the same options or risks the same crash.

`Azure.Storage.Blobs` is pinned to **12.26.0** in `HouseApp.Api.csproj` — kept at the version
that was live when the transfer-validation fix above was verified, deliberately not left to
float to latest untested. This does mean `BlobStorageService.GetUserDelegationKeyAsync` must
use the older 2-arg `(DateTimeOffset?, DateTimeOffset)` overload, not the newer
`BlobGetUserDelegationKeyOptions`-based one, which doesn't exist in 12.26.0.

### Property membership (multi-property, per-user)

A user can belong to 0:N properties. `Property.MemberUserIds` (a plain nullable string list on
the document) records who's connected — there is no separate join container. Membership is
**not** enforced via a Cosmos query predicate: `PropertiesController` fetches the whole
`properties` container with a plain `ToListAsync()` and filters **in memory** via the
`IsMember(property, userId)` helper, same reasoning as the other Cosmos footguns above
(translating `.Contains()` on a list property into Cosmos SQL is exactly the kind of query
shape that's bitten this project before, and the container will only ever hold a handful of
documents). Don't rewrite this as a `Where(p => p.MemberUserIds.Contains(userId))`
LINQ-to-Cosmos query.

`MemberUserIds` is `List<string>?`, not `List<string>` — genuinely nullable, not just
defaulted to `[]`. This field was added after properties already existed in production;
those older documents have no such JSON property at all, and Cosmos deserializes a missing
property as null, **not** the C# `= []` field initializer (this crashed `GetAll` in
production the first time — `.Contains()` on null — see
`PropertiesControllerTests.GetAll_SkipsPropertyWithNullMemberUserIds_WithoutThrowing`). Always
go through `IsMember()` rather than calling `.Contains()` on `MemberUserIds` directly. This is
a general pattern worth remembering: adding a required-looking field to an existing Cosmos
entity does not backfill it onto documents that predate the change — either null-check
defensively (as here) or write a one-time backfill if the field truly can't be absent.

**Every account is connected automatically when a property is created** (`PropertiesController.Create`
reads all of `db.Users` and stamps every id into `MemberUserIds`) — there's deliberately no
invite/sharing flow, since there are only ever 2 accounts and the app's whole premise is a
couple sharing visibility into the same house(s). A consequence: a user created *after* a
property already exists won't retroactively see it (covered by
`PropertiesControllerTests.GetAll_OnlyReturnsPropertiesCreatedWhileUserExisted`) — in practice
this can't happen since `DbSeeder` creates both accounts on first startup, before any property
can exist.

### Renovation types (admin-managed, not an enum)

What used to be a hardcoded `RenovationCategory` enum (Renovation/Maintenance/Furniture/Other)
is now admin-manageable data: `RenovationType` (its own container, `renovationTypes`, partitioned
by `/id`) with a `Name` and an optional `RecommendedIntervalMonths`, managed via
`RenovationTypesController` and `pages/RenovationTypesPage.tsx` (linked from a "Hantera typer"
button on the Renovations page, not the main nav — it's admin-adjacent, not a primary
destination). Both accounts can manage types equally; there's no separate admin role anywhere
in this app.

`RenovationTypesController.Delete` refuses (409) to delete a type still referenced by any
`RenovationEntry` — checked via the same full-scan-then-filter-in-memory pattern as everywhere
else, not a Cosmos query predicate.

**How the enum-to-dynamic-data migration avoided a data migration**: `RenovationEntry`'s field
was renamed from `Category` (enum) to `RenovationTypeId` (string), but is still mapped to the
JSON property `"Category"` (`ToJsonProperty("Category")` in `AppDbContext`) — so existing
entries' enum string values (`"Renovation"`, `"Maintenance"`, ...) are read unchanged as the new
field's value. This only works because `RenovationTypeSeeder` seeds the four default types using
those exact strings as their `Id` (not random GUIDs) — so old entries' references resolve to a
real, renamed-and-editable type with zero backfill. Unlike `DbSeeder`, this seeder runs once
ever (skips entirely if the container already has anything), not per-missing-item — re-adding a
type the admin deliberately deleted would be a bug, not idempotent seeding.

### Frontend property routing

Routes are property-scoped: `/properties` (picker — list your properties, or create one),
`/properties/:propertyId` (dashboard), `/properties/:propertyId/{valuations,renovations,documents,renovation-types}`.
`/` resolves via `RootRedirect` (`App.tsx`) to the last-viewed property
(`utils/lastProperty.ts`, backed by `localStorage`) or to the picker if there isn't one or it's
stale — the target route re-validates membership itself (via `useSelectedProperty`, which
redirects to `/properties` on a 404/not-a-member), so a stale localStorage id is harmless, not
a source of bugs. Every page under the property-scoped routes reads `propertyId` from
`useParams()`, not from a hook that "picks the first property" — there is no such hook
anymore. `NavBar` renders a property switcher (a `Menu` populated from `useProperties()`) that
preserves the current sub-page when switching (e.g. switching properties while on Valuations
stays on Valuations for the new property) by reusing the current path's suffix.

**`frontend/public/staticwebapp.config.json` is what makes those client-side routes survive a
page refresh** — it is not optional boilerplate. Without a `navigationFallback`, Azure Static
Web Apps looks for a physical file at e.g. `/properties/<guid>` and serves its own 404; the
routes only appear to work because in-app navigation never asks the server for them (this
shipped broken and was caught by refreshing a property page). It lives in `public/` so Vite
copies it to `dist/` — SWA reads it from the deployed output root, so putting it anywhere else
silently does nothing. The `exclude` list matters as much as the rewrite: `/api/*` must be
excluded so API calls still reach the linked backend instead of being handed `index.html`, and
static assets are excluded so a genuinely missing file 404s properly rather than returning HTML
under a `.js`/`.css` URL (which surfaces as a confusing MIME-type error, not an obvious 404).

### UI language

All user-facing frontend text (labels, buttons, headings, messages, empty states) is in
**Swedish** — the two users are Swedish-only. Code, comments, identifiers, API contracts, and
backend text stay in English (standard practice). Backend enum values
(`RenovationCategory`, `DocumentCategory`) are English strings by design — never rename them
to Swedish, since that's the wire contract — but their display labels are Swedish, defined in
`frontend/src/utils/labels.ts` (`RENOVATION_CATEGORY_LABELS`/`_OPTIONS`,
`DOCUMENT_CATEGORY_LABELS`/`_OPTIONS`). Use these maps for any new UI that displays or selects
a category rather than rendering the raw enum value. Number formatting is pinned to the
`'sv-SE'` locale explicitly (not `undefined`/browser-default) so output is consistent
regardless of the viewer's browser locale — see `formatCurrency`/`formatNumber` in
`DashboardPage.tsx`/`PropertyTimeline.tsx`. `index.html` sets `lang="sv"` to match. Dates are
left as plain ISO `YYYY-MM-DD` strings throughout (no locale formatting needed — this already
matches Swedish date convention).

### Test setup

`backend/tests/HouseApp.Api.Tests/HouseAppWebApplicationFactory.cs` boots the real
`Program.cs` pipeline but swaps `AppDbContext` for `UseInMemoryDatabase` and
`IBlobStorageService` for `FakeBlobStorageService`. Note it removes both
`DbContextOptions<AppDbContext>` **and** `IDbContextOptionsConfiguration<AppDbContext>` —
removing only the former isn't enough to override `AddDbContext`, since EF Core combines
multiple registered configurations rather than replacing them. `Program.cs` has an
explicit `EnvironmentName == "Testing"` branch to skip Blob-backed Data Protection (which
would otherwise try to hit real/emulated Azure Storage at startup).

### Infra composition (`infra/main.bicep`)

Module dependency order: `identity` → `keyVault`/`cosmos`/`storage`/`logAnalytics` →
`appInsights` → `appServicePlan`/`appService` → `staticWebApp`. Notable non-obvious
choices, don't "fix" these without re-reading why:
- App Service Plan defaults to **F1 (free)**, not a paid tier — chosen over Container Apps
  for deploy simplicity (plain `dotnet publish` + zip deploy, no Docker/registry) at
  effectively the same $0 cost. `alwaysOn: false` is required by F1.
- Cosmos DB is **Serverless capacity mode**, not the "free tier" discount — avoids the
  one-per-subscription free-tier constraint and has no idle floor or cold-start/resume
  delay (unlike SQL Serverless-style auto-pause).
- Static Web Apps only deploy in a limited set of regions — `staticWebAppLocation`
  defaults to `westeurope` independently of the `location` param used for everything else.
- The Static Web App SKU is **Standard**, not Free (~$9/mo) — this is required, not a
  choice: `linkedBackends` (the `/api/*` proxy the whole cookie-auth design depends on) is
  a Standard-tier-only feature and deployment fails with `SkuCode 'Free' is invalid` on
  Free. Don't downgrade this SKU without also removing the linked backend and rethinking
  auth (CORS + `SameSite=None` cookies).
- Storage CORS allows `*` for origins deliberately (see comment in
  `modules/storage.bicep`): security for direct browser-to-blob SAS uploads comes from the
  SAS signature/expiry, not origin restriction, since wiring the Static Web App's hostname
  back into the storage module would create a circular module dependency.
- The two seed accounts are passed as `@secure()` object params (`seedUser1`/`seedUser2`)
  from GitHub Actions secrets — never put real values in `infra/main.parameters.json`
  (committed placeholders are `"REPLACE_ME"`).

## CI/CD

Three independent GitHub Actions workflows (`.github/workflows/`), each path-filtered to
its own directory, using OIDC federated Azure login (no stored client secret):
- `backend-ci-cd.yml` — build/test always; publish + deploy to App Service on push to `main`.
- `frontend-ci-cd.yml` — build/lint always; deploy to Static Web Apps on push to `main`.
- `infra-deploy.yml` — `bicep build` + `what-if` always; `deployment group create` on push
  to `main`, gated behind the `production` GitHub Environment (manual approval required).
