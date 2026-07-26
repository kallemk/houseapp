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

Accounts are **admin-seeded only** — there is no public registration endpoint anywhere in
`AuthController`. Exactly two accounts will ever exist. `Data/Seed/DbSeeder.cs` creates
them from config (`Seed:Users` / `Seed__Users__N__*`) on every startup (idempotent — skips
if already present), in every environment except `Testing`. This seeding-on-every-startup
behavior is intentional and required for production: App Service has no separate
migration step, so this is the only place the two accounts ever get created.

Because the store is Cosmos DB (not relational), full ASP.NET Core Identity was dropped in
favor of a lightweight hand-rolled scheme: `PasswordHasher<ApplicationUser>` for hashing +
plain cookie authentication (`AddAuthentication().AddCookie()`, manual
`HttpContext.SignInAsync`/`SignOutAsync` in `AuthController`). Don't reintroduce
`UserManager`/`SignInManager`/`IdentityDbContext` — they assume a relational store.

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
them. Optionally encrypted at rest with `ProtectKeysWithAzureKeyVault` when `KeyVault:Uri` is
configured. Don't switch this back to Blob Storage without reading the git history first — it
was deliberately moved off Blob after `AzureBlobXmlRepository`'s key-ring read crashed every
login with `CryptographicException` → `InvalidQueryParameterValue` (empty `comp` parameter),
traced to the SDK's default download transfer validation being rejected by this storage
account (see the `CreateBlobClientOptions` note below) — moving key persistence to local disk
sidesteps the Blob SDK for this entirely rather than trusting that workaround.

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

### Frontend property scoping

The data model supports multiple properties, but the UI deliberately doesn't have a
property switcher — `hooks/usePrimaryProperty.ts` always uses the first property returned,
since in practice this will be the one house being tracked. If multi-property UI is ever
needed, this is the seam to extend.

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
