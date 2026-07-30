# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A private app for two people (a couple) to track their house: value over time, a log of
projects (maintenance, renovation and investment work), and document/photo storage. Personal-use scale (2 accounts,
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
`Project.CreatedByUserId` and `Document.UploadedByUserId`, and there is no migration
mechanism to rewrite them. Google sign-in therefore **matches an existing user by email
(case-insensitively) and never creates one** — if it minted new rows, both users would silently
lose sight of every existing property.

**The `users` container doubles as the sign-in allowlist.** A Google account is accepted iff a
user row with that email exists; otherwise `/api/auth/google` returns **403** (deliberately not
401 — the frontend distinguishes "not invited" from "sign-in failed"). Deleting a user in the
admin page is therefore how you revoke access.

### Roles: one `IsAdmin` flag, checked against the database

`ApplicationUser.IsAdmin` is the whole role model — regular is the default, admins additionally
manage users and property components. Two things about it are load-bearing:

**Enforcement reads the database, not a cookie claim.** `Authorization/AdminAuthorization.cs`
defines the `Admin` policy via an `AuthorizationHandler` that point-reads the user by
`ClaimTypes.NameIdentifier`. The conventional approach — adding a role claim in
`AuthController.SignInAsync` and using `[Authorize(Roles = ...)]` — was rejected because the auth
cookie lasts 14 days with sliding expiration: a demoted user would keep admin powers for up to two
weeks, and a promotion wouldn't apply until they logged out and back in. The handler is registered
**Scoped** (in `AddHouseAppCookieAuth`) so it can resolve the request-scoped `AppDbContext`. A
failed policy lands on the existing `OnRedirectToAccessDenied` hook and returns a clean 403.

**`DbSeeder` guarantees an admin exists, and that is not optional.** `IsAdmin` was added to an
entity whose documents already existed; a missing JSON property deserializes to `false`, so
deploying it makes *every* account regular and nothing in the UI can ever promote one — an
unrecoverable lockout on a store with no migration step. `DbSeeder` therefore (a) creates seed
accounts with `IsAdmin = true` and (b) **promotes every existing account if no admin exists at
all**. (b) only fires at zero admins, so it's inert after the first successful startup, and it's
the permanent recovery path if the admins are ever all lost. `UsersController.Update` refuses
(409) to clear the caller's *own* admin flag, which is what keeps "at least one admin exists" true
through the API: you can only demote someone else, and that requires still being one yourself.

`GET /api/property-components` is deliberately **not** gated (only POST/PUT/DELETE are) — it feeds
the component dropdown on the projects page and the dashboard quick-add modal, so gating it would stop
regular users creating entries at all. `UsersController` is gated in full, listing included.

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
- `valuationEntries`, `projects`, `documents`, `budgets` — partitioned by `/propertyId`
- `propertyComponents` — partitioned by `/id`
- `renovationEntries`, `renovationTypes` — the pre-project model, kept read-only as the migration's
  rollback path (see "The renovation → project migration")
  (so "all entries for a property" queries stay single-partition)

There are **no EF Core relational migrations**. Containers/partition keys are provisioned
by `infra/modules/cosmos.bicep` in Azure; locally, `Program.cs` calls
`db.Database.EnsureCreatedAsync()` in Development only. Schema evolution happens in
application code (tolerant reads), not via a migration step.

Controllers that update/delete a single item (`ValuationsController`,
`ProjectsController`, `DocumentsController`) require the `propertyId` (partition
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
  `PropertyId` on `ValuationEntry`/`Project`/`Document`) gets written under its literal
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

**Storing documents in Google Drive instead of Blob has been designed but not built** — see
`docs/google-drive-integration.md`. It's parked, and that document records why the obvious shapes
don't work (a folder URL grants no API access; service accounts have no Drive storage quota) so the
research doesn't get repeated. Two statements below would change if it's ever picked up: documents
would pass through the API, and the app would gain its first client secret.

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

**Creating a property connects only its creator**, and creating a user grants access to nothing.
Both used to do the opposite — `PropertiesController.Create` stamped every account into
`MemberUserIds` and `UsersController.Create` backfilled every new account onto every property. That
was the entire sharing model while there were two accounts and one house; it became a data leak the
moment a second household joined. Access now comes from exactly two places: being in
`MemberUserIds`, or the property being the demo.

**`Data/PropertyAccess.cs` is the only place that decides this, and every per-property controller
must call it.** `CanAccessPropertyAsync` (member *or* demo) guards everything that reads or writes a
property's contents; `IsPropertyMemberAsync` (strict) guards the things that must stay with the
owners — managing members and deleting. Both return `NotFound`, never `Forbid`, so a stranger can't
confirm a property exists.

This matters more than it looks: `ProjectsController`, `ValuationsController`, `DocumentsController`,
`BudgetsController` and `MaintenanceScheduleController` all take a `propertyId` from a route, query
string or **request body** and would otherwise act on it unchecked — including
`DocumentsController.GetUploadUrl`, where an unchecked id mints a SAS URL into someone else's blob
path. `PropertyAccessTests` walks every one of these endpoints; **a new per-property controller needs
adding there and needs the same call.**

**Any member manages that property's members** — there is no owner role. Adding someone hands them
the same keys you have, including the ability to remove you. Removing the *last* member is refused
(409): admins deliberately don't bypass membership, so an empty list would orphan the property and
its data permanently. Members are found through `GET /api/properties/{id}/member-candidates?query=`,
a substring search over name and email that is **scoped to a property you're already in, capped at
10, and silent below two characters** — enough to invite someone you know of, not a way to dump the
user directory, which `GET /api/users` still guards as admin-only.

**`Property.IsDemo` is a shared sandbox everyone can see *and edit*.** It exists so a newcomer with
no property of their own has something to learn on. Editable rather than read-only because that
teaches more and avoids hiding write controls on every page — the cost is that it accumulates other
people's experiments, which is the intended trade. It can't be deleted while flagged, and the flag
is **admin-only and deliberately not part of `SavePropertyRequest`** (`PUT /api/properties/{id}/demo`),
so a member can't publish their own house to everyone by editing it. Setting it clears the flag
elsewhere; there is only ever one demo. Its content is **curated in the app, never seeded** — a
startup seeder would need a marker document to avoid resurrecting a deleted demo, which is exactly
the trap `ProjectMigrator` fell into.

**Deleting a property cascades by hand.** Cosmos has no cascade delete and no cross-container
foreign keys, so `PropertiesController.Delete` explicitly removes the property's
`valuationEntries`, `projects` and `documents` (plus each document's blob, which lives
outside Cosmos entirely and nothing else would ever clean up). This isn't just tidiness: entries
are only ever reachable through their property, so orphans would sit in their containers
permanently and invisibly. Each child lookup is a single-partition `Where(x => x.PropertyId == id)`
query, since those three containers are partitioned by `/propertyId`. Blobs are deleted *before*
the rows, so a failure mid-way leaves the still-reachable documents intact rather than dropping
the only pointer to a leaked blob. Any new per-property container needs adding here too — see
`PropertiesControllerTests.Delete_AlsoRemovesValuationsProjectsAndDocuments`.

Editing and deleting properties is driven from `pages/PropertyPickerPage.tsx` (the per-card `⋮`
menu) rather than a settings page inside the property — that page is already the "manage your
properties" surface both `NavBar` entry points link to.

### Projects (the core domain model)

A `Project` is a piece of work on the house — planned, ongoing or finished. It replaced
`RenovationEntry`, which could only say "on this date we spent this much". Two classifications:

- **`WorkType`** (`Maintenance` / `Renovation` / `Investment`) — a hardcoded enum. This is what
  `DashboardPage` splits its totals on; before it existed, "Totalt investerat" summed every entry
  including routine upkeep and furniture.
- **`ComponentId`** → `PropertyComponent` — *which part of the house*. Admin-managed data in its own
  `propertyComponents` container (Tak, Fasad, VVS, …), deliberately not an enum so the list is
  editable in-app. `PropertyComponentsController` mirrors the old renovation-types shape exactly:
  `GET` open to every signed-in user (it feeds the dropdown when creating a project — gating it
  would stop regular users logging work at all), mutations admin-only, and `Delete` refusing (409)
  to remove a component any project still references.

**Costs and contractor are EF owned types, stored as nested JSON inside the project document** —
`OwnsOne(p => p.Contractor)` / `OwnsMany(p => p.Costs)` in `AppDbContext`. They are never read
without their project, so nesting means one read per project and no cross-container joins (which
the Cosmos provider doesn't do anyway). Consequences worth knowing:
- A project is read and written **whole**. `ProjectsController` has no sub-resource endpoints for
  costs; `Update` replaces the whole cost list. Don't add `POST /projects/{id}/costs`.
- There is no way to query costs across projects without reading the projects.
- `ContractorInfo` is per project — the same firm on two jobs is stored twice. A reusable contractor
  register would need its own container.

**`Project.ActualCost` is computed, not stored** (`Costs.Sum(...)`, with `Ignore()` in the model
configuration). One source of truth; a stored total would drift from the rows. The UI shows the
estimate until there's at least one cost row.

### Derived data: the maintenance schedule and budget actuals

Two features deliberately store less than they display, for the same reason `Project.ActualCost`
isn't stored — a second copy of something is a copy that goes stale.

**The maintenance schedule has no container at all.** `MaintenanceScheduleController` computes
`GET /api/properties/{id}/maintenance-schedule` from `PropertyComponent.RecommendedIntervalMonths`
plus the newest **completed Maintenance** project for each component. Every field the original
sketch wanted to store (`LastCompletedDate`, `NextDueDate`, `IsCompleted`) is derivable, so storing
them would mean editing a project's date silently left the schedule wrong. Rules worth keeping:
- No interval on the component → `NotScheduled`.
- **`MaintenanceBaseline` says where `LastCompletedDate` came from, and the UI must keep them
  distinct.** `Project` = real logged work. `YearBuilt` = nothing logged, so the property's build
  year stands in (the component is assumed to date from the house). That's a starting point to
  correct by logging work, *not* a record that anything was done — the maintenance page marks it
  "Antaget byggår" for exactly that reason. `None` = neither, which stays `Unknown` rather than
  overdue, because calling it overdue would state a guess as a fact.
- Expect the `YearBuilt` baseline to make an old house look comprehensively overdue on day one.
  That's intended: it's the backlog, and it shrinks as real work gets logged.
- **`Project.ExcludeFromMaintenanceSchedule` hides a project from the schedule entirely** — set for
  work too minor to count, like patching a few tiles. It's phrased as an *exclusion* deliberately:
  the field was added after projects existed, and a missing JSON property deserializes to `false`,
  so false has to mean "behaves as before". A positive `CountsToward…` flag would have silently
  emptied the whole schedule on the deploy that shipped it. The UI flips it back to a ticked
  "Räknas mot underhållsplanen", so the awkward direction stays in storage. It applies in both
  directions — an excluded project sets neither the baseline nor `HasUpcomingProject`.
- **`Maintenance` and `Renovation` both reset the clock** (`LifeExtendingWork`), with
  `Status.Completed`. Renovating or replacing a part extends its life at least as much as servicing
  it, so a roof redone last year isn't due just because it was logged as a renovation. `Investment`
  does not count — that's new capital work, not upkeep of the existing part. A planned job hasn't
  happened, so it only sets `HasUpcomingProject`.
- The endpoint takes an optional `asOf` date purely so tests can assert Overdue/DueSoon against a
  fixed point rather than whenever the suite runs.

**`Budget` stores only the three budgeted amounts.** The sketch also had
`ActualMaintenanceSpent`/`ActualRenovationSpent`/`ActualInvestmentSpent`; those are summed from
`ProjectCost` rows on read instead. A cost belongs to the year of its own `DateIncurred`, **not**
the project's completion date, so a job spanning New Year splits across both years the way the
money did. `GetAll` returns every year with a budget *or* any spend, so money spent without a plan
is visible rather than hidden behind a missing row.

**`Project.Milestones` is `List<ProjectMilestone>?`, genuinely nullable** — it was added after
projects already existed, and a missing JSON property deserializes to null rather than running the
`= []` initializer (the same trap as `Property.MemberUserIds`). Always read it as `?? []`. `Costs`
does *not* need this: every create/update has written it since the container existed. Milestones
carry no money — the sketch had cost-per-stage fields, which would have recreated exactly the
two-sources-of-truth problem `ActualCost` avoids.

**`Document.Title` is an optional human label; `FileName` is what's on disk.** Filenames like
"scan_0042.pdf" say nothing, so the UI shows `title ?? fileName` everywhere and the documents page
keeps the filename visible underneath. Blank titles are normalised to null on write so that fallback
works. Titles are set at upload only — there's no edit-document UI yet.

**Documents attach to projects by `Document.ProjectId`, saved immediately — not with the project
form.** Documents live in their own container, so `components/projects/ProjectDocuments.tsx` uploads
and attaches straight away rather than waiting for the surrounding form to be submitted; that's also
why it only renders for a saved project, never while creating one. `PUT /api/documents/{id}/project`
attaches or (with null) detaches an existing document.

That field is still stored as JSON `"RenovationEntryId"` (`ToJsonProperty` in `AppDbContext`), since
the `documents` container was never migrated. **The frontend kept posting `renovationEntryId` after
the rename, so it never bound to `ProjectId` and every attachment silently became null** — covered
now by `DocumentsControllerTests.Create_WithProjectId_KeepsTheLink`.

**Every table is wrapped in `Table.ScrollContainer`** with a `minWidth`, so narrow screens scroll the
table horizontally instead of squashing or clipping it. New tables need the same treatment — the
mobile layout has no other way to show a wide table.

**The dashboard map is a keyless OpenStreetMap iframe** (`components/dashboard/PropertyMap.tsx`),
driven by `Property.Latitude`/`Longitude`. No map library and no provider key: Leaflet would be a
dependency for one static pin, and Google/Azure Maps/Mapbox each need a billing account and another
secret to deploy. Coordinates are **stored, not geocoded on render** — geocoding is a third-party
call that can fail or rate-limit, and the answer never changes once it's right.
`utils/geocode.ts` fills the fields from the address via Nominatim behind an explicit button; it is
a convenience, and the stored values remain the source of truth and stay editable. Both null ⇒ no
map is rendered.

**The dashboard timeline groups by year and expands to quarters on click.** A house owned for 20
years is 80 quarter rows, nearly all empty. `utils/quarters.ts` keeps both groupings; the year is
the default and the quarter view is opt-in per year. Both levels carry the same quick-add menu, just
seeding a different default date. It shows **valuations and projects only** — documents were dropped
because they crowded out the two things worth seeing, and the quick-add went with them so nothing
can be added there that then leaves no trace. Every row is dated and links somewhere.

### The renovation → project migration (done, and removed — don't re-add it)

**`ProjectMigrator` no longer exists, and nothing should replace it.** It copied `renovationEntries`
into `projects` on every startup, guarded by "copy the entries whose id isn't already in `projects`".
That guard cannot distinguish **"never copied"** from **"copied, then deliberately deleted"** — so
every project deleted in the UI came back on the next deploy, restored from the still-intact legacy
container. It shipped, it happened in production, and it was deleted rather than repaired.

The lesson generalises: **a one-shot migration must not run on a schedule.** Inferring "has this
already run?" from the state of the data is what breaks, because user deletions are indistinguishable
from work not yet done. If another migration is ever needed, it has to *record that it ran* — a
marker document that's written after the copy completes, checked before starting — so a rerun is a
no-op regardless of what has since been deleted. (`DbSeeder` and `PropertyComponentSeeder` are safe
for a different reason: they only ever add missing *seed* rows, and `PropertyComponentSeeder`
explicitly skips the whole container once anything is in it, precisely so a deleted component isn't
re-added.)

What the migration did, recorded because the data it produced is still in production:

- **It copied, it didn't move.** `renovationEntries` and `renovationTypes` are still provisioned in
  `infra/modules/cosmos.bicep` and still hold every original document. That is the rollback path —
  **don't delete those containers, or `Data/Migration/LegacyModels.cs`, until reverting is off the
  table.** `PropertiesController.Delete` deliberately doesn't cascade into them. (This is also what
  made the resurrection bug above possible: a complete, untouched source to re-copy from.)
- **Ids were preserved.** `Document.ProjectId` is the old `RenovationEntryId` field (mapped via
  `ToJsonProperty("RenovationEntryId")`, since the `documents` container wasn't migrated), so
  regenerating ids would have orphaned every document attached to a renovation. Those ids are load-
  bearing today, not just during the migration.
- **Old types mapped onto `WorkType`, not onto components** — they classified the work, not the part
  of the house. The four seeded ids mapped explicitly; an admin-created type couldn't be inferred, so
  it became `Renovation` with its original name appended to `Notes`. If you see "Tidigare typ: …" in
  a project's notes, that's why.

**New containers must be deployed before the code that reads them.** The app can't create them:
production authenticates with a managed identity holding only a data-plane role. `backend-ci-cd` and
`infra-deploy` are path-filtered and independent, so a single push touching both runs them *in
parallel* — push the Bicep change and let `infra-deploy` finish first.

### Frontend property routing

Routes are property-scoped: `/properties` (picker — list your properties, or create one),
`/properties/:propertyId` (dashboard), `/properties/:propertyId/{valuations,projects,maintenance,budget,documents}`,
plus `/properties/:propertyId/projects/:projectId` for the project detail form (`new` = create mode)
and `/properties/:propertyId/admin/{components,users}` (see below).
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

**The Administration section (`/admin`) is reachable by everyone, and gates per page rather than at
the route.** `pages/AdministrationPage.tsx` is a layout route holding the shared heading and the tab
bar; each management page renders through its `Outlet`, and `/admin` alone redirects to the first
tab. There used to be an `auth/AdminRoute.tsx` that bounced non-admins to `/properties` — it's gone,
because the section now mixes pages with different rules: components is read-only for a regular user
(the list is the vocabulary the projects page is built on, so it's worth reading), while users shows
a plain "no permission" state. Silently redirecting away from a nav link everyone can see is worse
than saying why. `useUsers(enabled)` takes a flag purely so the users page doesn't fire a request it
knows will 403. **New management pages belong here as another tab**, and each one decides its own
non-admin behaviour — the API is still the real gate either way.

"Hantera komponenter" links there from the **maintenance** page, not the projects page: the schedule
is computed directly from the components' recommended intervals, so that's where wanting to change
one actually arises.

### UI language

All user-facing frontend text (labels, buttons, headings, messages, empty states) is in
**Swedish** — the two users are Swedish-only. Code, comments, identifiers, API contracts, and
backend text stay in English (standard practice). Backend enum values
(`WorkType`, `ProjectStatus`, `ProjectPriority`, `CostType`, `PropertyType`, `DocumentCategory`)
are English strings by design — never rename them
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
  a Standard-tier-only feature ([plans](https://learn.microsoft.com/en-us/azure/static-web-apps/plans)
  lists Free as managed Functions only) and deployment fails with `SkuCode 'Free' is invalid`
  on Free. **The error message is misleading, and the obvious search result is a red herring:**
  that error is most commonly caused by a `SystemAssigned` identity on the Static Web App, which
  Free doesn't support — `modules/staticWebApp.bicep` has no `identity` block at all, so that fix
  doesn't apply here. It's the `linkedBackends` child resource. Nor can
  `staticwebapp.config.json` stand in for the proxy: `rewrite` values
  [must be relative to the app root](https://learn.microsoft.com/en-us/azure/static-web-apps/configuration),
  so there's no external-host proxying.

  This was re-examined in July 2026 with the explicit goal of getting to Free, and the answer
  was to stay on Standard. Free *does* support custom domains (2 per app), so the domain isn't
  what forces the SKU. What was rejected, and why:

  | Alternative | Cost | Why not |
  |---|---|---|
  | Serve the SPA from the App Service, delete the SWA | $0 | Loses `housetracker.odenbulten.se` — App Service **F1 can't have a custom domain** ("make sure that your App Service app isn't in the Free tier"), and B1 to regain one is ~$13/mo, more than the SWA costs |
  | SWA Free + CORS + `SameSite=None` | $0 | Cookies become third-party; Safari blocks those by default, so sign-in would likely fail on iOS — the main way this app gets used |
  | Free proxy in front (Cloudflare Worker etc.) routing `/api/*` | $0 | Keeps both the domain and same-origin, but adds a non-Azure moving part outside Bicep |
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
- `backend-ci-cd.yml` — build/test always; publish + deploy to App Service on push to `main`,
  **followed by an explicit `az webapp restart`**. That restart is load-bearing: zip deploy
  overwrites the DLLs in `/home/site/wwwroot` (an Azure Files mount) while the old process still
  has them memory-mapped, so the first method JIT'd after the swap reads the new file at stale
  offsets and throws `BadImageFormatException: Bad binary signature`. It surfaces as a 500 from
  whichever endpoint is called first after a deploy — with a stack that dies in
  `ObjectMethodExecutor..ctor` *before* the controller is even constructed, so it looks nothing
  like an application bug. Don't remove the restart step. (`WEBSITE_RUN_FROM_PACKAGE=1` would fix
  this more fundamentally, and would also stop zip deploy leaving deleted files behind in
  `wwwroot` — deliberately not adopted yet, the restart was the smaller change.)
- `frontend-ci-cd.yml` — build/lint always; deploy to Static Web Apps on push to `main`.
- `infra-deploy.yml` — `bicep build` + `what-if` always; `deployment group create` on push
  to `main`, gated behind the `production` GitHub Environment (manual approval required).
