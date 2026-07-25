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
auth encrypts the session cookie with the Data Protection key ring, which by default lives
on local disk. `AddHouseAppDataProtection` persists it to Blob Storage
(`PersistKeysToAzureBlobStorage`) so restarts/idle-unloads on App Service don't silently
invalidate every login. Don't remove this even though nothing else references it directly.

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
