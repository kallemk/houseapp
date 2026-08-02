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
(case-insensitively) before considering creating one**, and that ordering is the load-bearing part:
if a returning person got a fresh id, they would silently lose sight of every property they belong
to. Pinned by `GoogleLogin_Twice_ReusesTheSameAccount` and
`GoogleLogin_ForAnExistingPasswordAccount_KeepsItsId`.

**The `users` container is no longer a sign-in allowlist — registration is open.** The app is
published on Google, so `/api/auth/google` **creates an account** for any verified email it doesn't
recognise (regular, never admin). It used to answer 403 to anyone not already in the container, and
a lot of the surrounding design still assumes small, known membership — read the next paragraph
before adding anything that trusts "signed in" to mean "someone we know".

**`ApplicationUser.IsBlocked` is what revocation means now, and deleting a user is not.** A deleted
account simply reappears on that person's next sign-in, so `Delete` is for tidying up, not for
keeping someone out. Blocking is refused at both sign-in paths (403) **and enforced per request** by
`OnValidatePrincipal` in `AddHouseAppCookieAuth`, which rejects the principal and signs the session
out — the cookie is a 14-day sliding session, so without that a block would take effect up to a
fortnight later. Same reasoning as `AdminAuthorizationHandler` reading `IsAdmin` from the database
rather than a claim, and it matters more here: that one governs extra powers, this one governs access
at all. The cost is a point read per authenticated request (~1 RU on Cosmos). That same check makes
*deletion* take effect immediately too, since a missing user is rejected as well. `UsersController`
refuses to let you block yourself, for the same reason it refuses self-demotion and self-deletion.

**What open registration exposes, and what was accepted:** a brand-new account is a member of no
property, so it sees only the demo — which is **editable by everyone**, and "everyone" is now the
internet rather than a handful of invited people. That was a deliberate call (the demo is a sandbox
and accumulating other people's experiments is the point). Likewise `member-candidates` still
substring-searches names and emails, so anyone who creates a property can use it to explore the user
directory; also deliberate, because inviting someone whose address you half-remember matters more
here than hiding a list of names. Both are worth revisiting if the app attracts strangers rather
than friends-of-friends.

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

`GET /api/property-components` is deliberately **not** gated (only POST/PUT/DELETE are) — it's the
registry every property inherits from, and the components page shows it read-only to everyone.
`PropertyLocalComponentsController` isn't admin-gated at all: a property's own component list is
membership-scoped, not an admin concern. `UsersController` is gated in full, listing included.

Google sign-in uses the **ID-token flow**, not a server-side OAuth redirect: the browser gets a
token from Google Identity Services and posts it to the API, which verifies it via
`IGoogleTokenValidator` (`Google.Apis.Auth`). **Sign-in therefore uses no client secret** and there's
no `AddGoogle()` handler. It was chosen when the frontend was a Static Web App proxying to the API, where a redirect flow
would have bounced to the App Service hostname and landed the session cookie on the wrong domain.
That constraint is gone now everything is one origin, but the ID-token flow is kept: it's simpler
and needs no client secret. `IGoogleTokenValidator` is an interface purely so tests can
substitute `FakeGoogleTokenValidator`, exactly as `IBlobStorageService` is handled.

**The Google Drive integration does use a client secret** (`Authentication:Google:ClientSecret`, from
the `GOOGLE_CLIENT_SECRET` GitHub *secret* — never a repo variable, unlike the public client id).
That's the app's only stored credential, and it's confined to Drive: sign-in never touches it. See
"Documents" below for why Drive can't use the ID-token shortcut.

`ApplicationUser.PasswordHash` is **nullable**: users added for Google-only access have none, and
`Login` must reject a null/empty hash rather than feeding it to the hasher.

New users must be **backfilled onto existing properties** (`UsersController.Create` does this).
`PropertiesController.Create` only stamps the users existing *at that moment* into
`MemberUserIds`, so without the backfill an invited person signs in to an empty property list.

Cookie config in `Extensions/ServiceCollectionExtensions.cs` (`AddHouseAppCookieAuth`) uses
`SameSiteMode.Lax` and `CookieSecurePolicy.SameAsRequest` on purpose:
- **Lax + no CORS works** because the frontend and backend are same-origin in both
  environments. In production that is now literal rather than arranged: the App Service serves the
  SPA from its own `wwwroot`, so there is one origin and nothing to proxy. Locally the Vite dev
  proxy forwards `/api` to the backend (`vite.config.ts`). If you ever see CORS errors, the fix is
  to restore single-origin serving, not to add a CORS policy. Don't use `CookieSecurePolicy.Always`.

**`AuthController.SignInAsync` must keep passing `AuthenticationProperties { IsPersistent = true }`,
and `ExpireTimeSpan` is not a substitute.** Those two settings look like they cover cookie lifetime
and don't: `ExpireTimeSpan`/`SlidingExpiration` bound the *ticket encrypted inside* the cookie, while
`IsPersistent` is what makes the handler emit `Expires`/`Max-Age` at all. Without it every sign-in
issues a **session cookie**, the app still works perfectly in every test and on desktop (Chrome's
"continue where you left off" restores session cookies), and Android users get logged out constantly
because the OS kills the browser and nothing restores them. It shipped that way and was only found by
someone noticing they signed in more often on their phone. `AuthControllerTests` asserts the raw
`Set-Cookie` header carries an expiry for both sign-in paths, because the failure is an *absent*
attribute that nothing else notices.

**Transient unavailability is retried** (`api/client.ts`). This was written for F1 cold starts,
which `alwaysOn: true` on B1 has since removed — but it still earns its place: every deploy restarts
the app (see `ci-cd.yml`), and the SPA now lives on that same App Service, so a reload mid-deploy
hits a starting server. `request()` retries network errors and 502/503/504 — four attempts, backing off — but **only for GET
by default**: a POST that times out may still have been processed, and retrying a Drive upload would
create the file twice. The two sign-in calls opt in explicitly (`api/auth.ts`), since they're the
requests most likely to meet a sleeping backend and are safe to repeat. 401/403/404/409 are never
retried — they're answers. Before this, a cold start surfaced on the login page as *"Google-inloggningen
misslyckades. Försök igen."*, which told the user to do the one thing that looked like it helped.

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

**Blob documents never pass through the API**: `DocumentsController.GetUploadUrl` /
`GetDownloadUrl` issue short-lived SAS URLs (`Services/BlobStorageService.cs`), and the
browser PUTs/GETs directly to Blob Storage. `BlobStorageService` transparently supports
both a shared-key SAS (local dev via Azurite connection string,
`BlobClient.CanGenerateSasUri == true`) and a user-delegation SAS (production, managed
identity, no account key) — same code path, branched at runtime. **Drive documents are the
exception** — see below.

**Data Protection key persistence is load-bearing, not optional infrastructure**: cookie
auth encrypts the session cookie with the Data Protection key ring. `AddHouseAppDataProtection`
persists it to **local disk** (`PersistKeysToFileSystem`), not Blob Storage — on App Service
Linux this resolves to `/home/data-protection-keys`, which is persistent across
restarts/idle-unloads for a single instance, so logins survive them. **This is only correct while
the plan runs exactly one instance, and that is no longer guaranteed by the tier.** F1 could not
scale out; the shared B1 can, and scaling is per *plan* — another app on `plan-common-001` could
trigger it. If it ever runs more than one instance the key ring diverges and people get signed out
at random as requests land on different instances. Keep it at one instance, or move key persistence
to Blob/Key Vault before scaling. **Not** encrypted at rest with Key Vault — `ProtectKeysWithAzureKeyVault` needs a URI to
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

### Documents in Google Drive (opt-in, per property)

A property's documents go to Blob Storage unless a member connects Google Drive, after which new
uploads go to a folder in *their* Drive. `docs/google-drive-integration.md` holds the research; the
decisions that constrain the code:

**The scope is `drive.file`, and that choice shapes everything else.** It's non-sensitive (basic
verification, no security assessment), unlike `drive`/`drive.readonly` which are restricted. The
price is that it only reaches files the app itself created — which is why **the app creates the
folder** rather than accepting a pasted folder URL (a URL grants no API access anyway), and why a
file dropped into the folder from Drive's web UI stays invisible to the app. Accepted, not a bug.

**One connection per property, not per member**, forced by the same scope: each user would only see
files *they* created, so two members uploading under their own grants would each see half the folder.
`Property.GoogleDriveConnectedByUserId` names whose token every upload uses; the token itself lives on
`ApplicationUser.GoogleDriveRefreshTokenProtected` so one person connecting three properties needs one
grant. `Services/DriveAccessTokenResolver.cs` is the only place that turns a property into a token.

**This is a real redirect OAuth flow** (`DriveAuthController`), the thing sign-in deliberately avoids.
It survives `SameSite=Lax` because `/api/drive/connect` is reached by **top-level navigation**, which
Lax allows, and the callback returns to the same public origin — so
`Authentication:Google:DriveRedirectUri` must be the custom domain bound to the App Service
(`https://housetracker.odenbulten.se/api/drive/callback`), never the App Service hostname, and
`http://localhost:5173/api/drive/callback` locally (the Vite proxy's origin, not the backend's).
Both must be registered in the Google Console.

**The `state` parameter is the CSRF defence** and carries the property and user, so the callback
trusts nothing in the query string but the code. Both it and the refresh token go through
`IDriveTokenProtector`, reusing the Data Protection key ring — which extends that key ring's
blast radius: losing it already meant re-signing in, and now also means reconnecting Drive.
`prompt=consent` + `access_type=offline` are both required or Google returns no refresh token; the
callback **refuses to connect at all** rather than storing an access token that dies in an hour.

**`DocumentStorageKind.Blob` must stay 0 and the enum is append-only** — EF Cosmos stores it as an
int and every pre-existing document has no such property, so 0 is what makes this migration-free.
Same trap as `DocumentCategory`.

**The folder structure is built lazily, and `Services/DriveFolderResolver.cs` owns all of it:**

```
HusTracker – {property}
  Allmänt                        ← documents with no project
  Projekt
    {project name} {yyyy-MM-dd}  ← one per project, created on its first upload
```

`Allmänt`/`Projekt` are created up front on connect so the folder looks organised immediately, but
the resolver still creates them on demand — properties connected before this existed have a root and
nothing else, and `Property.GoogleDriveGeneralFolderId`/`ProjectsFolderId` are null there. Project
folders are created on the project's *first* upload, not with the project: most projects never get a
document, and the property may not have been on Drive when the project was made. The date suffix is
`Project.CreatedAt`, not today's date, so the name doesn't depend on when the first document happened
to arrive. Ids are saved the moment a folder is made, before the upload that prompted it, so a failed
upload can't orphan one.

**The structure is kept in step after the fact, and the two cases fail differently on purpose:**
- `PUT /api/documents/{id}/project` **moves the file** into the project's folder, or back to
  "Allmänt" when detached. The move happens *before* the metadata is saved and a dead connection
  **refuses** the whole call (409), because the alternative is recording an attachment whose file is
  filed somewhere else.
- `PUT /api/projects/{id}` **renames the folder** when the name changes, keeping the same date suffix.
  This one is **best-effort after the save** and only logs on failure: a folder name is cosmetic, the
  files in it are already right, and refusing to rename a project because Drive is unreachable would
  be a poor trade. A later rename fixes it.

**Disconnecting clears every project's `GoogleDriveFolderId` too**, not just the property's. They
point into the tree being forgotten, and reconnecting builds a fresh one — stale ids would file new
documents into the old structure, outside the new root, where nobody would look.

**Drive uploads pass through the API** (`POST /api/documents/upload`, multipart), because there's no
equivalent of a SAS URL without handing the browser a Drive token. Capped at 25 MB — originally
because those bytes shared F1's 60 CPU-min/day quota, which B1 doesn't have; the cap stays as a
sanity bound on a request that streams through the API. **The server decides the path, not the client**:
`POST /api/documents/upload-url` returns `Sas` or `Drive`, so `FileUpload` and its three call sites
are backend-agnostic, and each write endpoint returns 409 for a property on the other backend so a
stale client can't write a row pointing at a file nobody uploaded.

**Deleting from Drive is opt-in per action**, on both the document and the property cascade, and
defaults to off — the files are in someone's personal Drive. Disconnecting never touches the folder
or its files, and documents uploaded while connected keep opening afterwards via the stored
`DriveWebViewLink` (stored at upload precisely so opening needs no Drive call and no live grant).

**A dead grant is a 409 with `code: "drive_connection_expired"`, not a 500** — nothing is broken, the
connection needs remaking, and the UI says so.

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

- **`WorkType`** (`Maintenance` / `Renovation` / `Investment` / `Purchase`) — a hardcoded enum, and
  **append-only**: EF Cosmos stores it as an integer (the string values are only the wire contract),
  so reordering reclassifies every stored project. This is what `DashboardPage` splits its totals on;
  before it existed, "Totalt investerat" summed every entry including routine upkeep and furniture.
  The four are meaningfully different and each is worded in `WORK_TYPE_DESCRIPTIONS`
  (`utils/labels.ts`), shown in every work-type dropdown via `components/common/WorkTypeSelect.tsx` —
  picking the wrong one silently moves money between dashboard figures, so the distinction isn't left
  to be guessed:
  - `Maintenance` — bevara eller ersätta befintligt
  - `Renovation` — förbättra befintligt
  - `Investment` — tillföra något nytt till fastigheten
  - `Purchase` — köpa lös egendom eller utrustning

  **`Purchase` is budgeted like the rest but is deliberately excluded from "Mot insatt kapital"** on
  the dashboard: it buys movable things that can leave with you, so it doesn't raise what the
  property is worth. That figure is an **allowlist** (`Renovation` + `Investment` in `DashboardPage`),
  not an exclusion list, so a future work type has to be argued into it rather than landing there by
  default. `Purchase` is out of the maintenance schedule for the same reason `Investment` is — buying
  a mower doesn't service anything.
- **`ComponentId`** → a component — *which part of the house*. Admin-managed data (Tak, Fasad, VVS, …),
  deliberately not an enum so the list is editable in-app, and since split into a central registry
  plus a per-property copy — see below.

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

### Components: a central registry each property can diverge from

Components exist at two levels, because a component list that's right for one house is wrong for the
next — a flat has no roof, and one household's roof interval isn't another's.

- **The central registry** — `PropertyComponent`, the `propertyComponents` container, managed by
  admins on the Administration page. `PropertyComponentsController` mirrors the old renovation-types
  shape: `GET` open to every signed-in user, mutations admin-only, `Delete` refusing (409) to remove
  a component any project references.
- **A property's own list** — `Property.LocalComponents`, an `OwnsMany` collection nested in the
  property document (no container of its own: it's never read without the property and holds a dozen
  rows). Managed by any member on `/properties/:id/components` via
  `PropertyLocalComponentsController`. **Not admin-gated** — it affects one property and only the
  people already in it, so the check is the ordinary `CanAccessPropertyAsync`. Admin rights govern
  the shared registry, not what someone does inside their own house.

Four things about this are load-bearing:

**`Property.ComponentsCustomized` is what makes it safe, and it must not be replaced by inferring the
same thing from an empty list.** False = the property follows the central registry and its effective
list *is* whatever `propertyComponents` currently holds (nothing is stored, so it keeps picking up
central edits and additions for free). True = only `LocalComponents` counts. Deleting every local
component is a legitimate thing to do, and reading `LocalComponents is null or []` as "not
customized" would restore the central set on the next page load — the same "can't distinguish 'never
ran' from 'deliberately emptied'" mistake that made `ProjectMigrator` resurrect deleted projects. A
plain non-nullable bool, so properties predating the field read as false, which is exactly how they
behaved before.

**A local copy keeps the central component's `Id` rather than getting a fresh one.** That is what
makes the whole feature free of migration: `Project.ComponentId` already holds central ids, and every
one keeps resolving once a property takes its own set. It also means "does central still have this
id?" is the complete answer to where a row came from, so `ComponentOrigin`
(`Central`/`Modified`/`Local`) is computed on read by comparing to the live central values — no
snapshot to go stale. A component invented locally gets a fresh Guid, which can't collide.

**`Data/PropertyComponentSet.cs` is the only place that branches on the flag.** Everything else —
`MaintenanceScheduleController`, the project component dropdown, the property's component page —
calls `GetEffectiveComponentsAsync(property)` and can't tell the two cases apart. `EnsureCustomizedAsync`
materialises the *whole* central set before any local edit, so changing one interval doesn't silently
drop every other component.

**Sync (`POST .../components/sync`) overwrites both-sides rows from central and adds central
components the property lacks, leaving `Local` rows alone** — they're either the property's own
additions or ones central has dropped, and both may have projects logged against them. It's a no-op
on an uncustomized property: it already *is* the central list, and materialising a copy would only
stop it tracking future central changes, so the UI hides the button until there's a local list.

Deleting a local component is refused (409) if one of **that property's** projects uses it; another
household's projects are none of its business. There is no per-property "postpone this activity"
— lengthening or clearing the interval locally is how that's expressed today (a cleared interval
makes the component `NotScheduled`).

### Kommande utgifter (the upcoming-expense bars on the budget page)

`components/budget/UpcomingExpenses.tsx` shows what unfinished work is going to cost, grouped by
year and expanding into quarters. It sits below the budget table on purpose: the budget is what you
decided to spend, this is what the projects you've already entered will cost, and reading them in
that order is the point.

- **Counts every open status** — `Planned`, `InProgress` *and* `OnHold`. On-hold work is the easiest
  to forget precisely because it's paused, and it's still money ahead of you. Completed work has
  already been paid and cancelled work never will be.
- **Uses `estimatedCost`, not cost rows.** An unpriced project counts as 0, so the total is a lower
  bound — the card says so, otherwise a 0 reads as "free" rather than "nobody has priced it".
- **Only years that have work get a row.** A continuous range would render a decade of empty rows
  the moment someone plans a roof for 2036.
- **Past years are kept, not folded into the current one**, and badged *Försenat*. Open work planned
  for last year is worth seeing as exactly that rather than being quietly relabelled as upcoming.
- Undated work gets its own "Utan datum" row rather than being dropped — it still costs money.
- **Drawn with `Progress` bars, not a charting library.** Adding `@mantine/charts`/Recharts would
  have cost 100 KB+ gzipped on a bundle already past Vite's warning threshold, for one screen; the
  same bar treatment `SpendByComponent` uses cost ~20 KB and matches the rest of the app.

### Derived data: the maintenance schedule and budget actuals

Two features deliberately store less than they display, for the same reason `Project.ActualCost`
isn't stored — a second copy of something is a copy that goes stale.

**The maintenance schedule has no container at all.** `MaintenanceScheduleController` computes
`GET /api/properties/{id}/maintenance-schedule` from the **property's effective components'**
`RecommendedIntervalMonths` (via `GetEffectiveComponentsAsync`, so a locally adjusted interval wins
over the central one) plus the newest **completed Maintenance** project for each component. Every field the original
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
works. `PUT /api/documents/{id}` edits the title, date and category after upload —
`components/documents/EditDocumentModal.tsx`, offered from both the documents page and a project's
attachment list. **Metadata only, and deliberately so**: `FileName`, `ContentType` and `SizeBytes`
describe the stored file and would start lying if they were editable, and on Drive the title is the
app's label rather than the file's name (the file keeps the filename it was uploaded under, so
nothing there needs renaming). A title is required on edit as well as on upload, which is where
documents predating titles finally get one.

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

**The property map is a keyless OpenStreetMap iframe** (`components/properties/PropertyMap.tsx`). No
map library and no provider key: Leaflet would be a dependency for one static pin, and
Google/Azure Maps/Mapbox each need a billing account and another secret to deploy. Coordinates are
**stored, not geocoded on render** — geocoding is a third-party call that can fail or rate-limit, and
the answer never changes once it's right. `utils/geocode.ts` fills the fields from the address via
Nominatim behind an explicit button; it is a convenience, and the stored values remain the source of
truth and stay editable.

It lives **inside `PropertyForm`, not on the dashboard**, and renders from the live form values
rather than the saved property — its job is to confirm the coordinates point at the right house
before they're saved, which is precisely what the geocode button can get wrong. On the dashboard it
was decoration that pushed the figures down the page. Either field empty or non-numeric ⇒ no map.

**The dashboard timeline groups by year and expands to quarters on click.** A house owned for 20
years is 80 quarter rows, nearly all empty. `utils/quarters.ts` keeps both groupings; the year is
the default and the quarter view is opt-in per year. Both levels carry the same quick-add menu, just
seeding a different default date. It shows **valuations and completed projects only** — documents
were dropped because they crowded out the two things worth seeing, and the quick-add went with them
so nothing can be added there that then leaves no trace. Every row is dated and links somewhere.

**Planned and in-progress work is deliberately absent.** The timeline is a record of what *happened*,
so `projectDate` returns null for anything that isn't `Completed`. Including plans made intentions
look like history — a job planned for later this year sat among the things actually done, and one
planned for a future year vanished silently, since the timeline only enumerates up to the current
year. Open work has its own card on the dashboard, which is where a plan belongs. Note that
`QuickAddModal` creates projects as `Completed`, so quick-added items still appear.

**Runs of three or more empty years collapse into one expandable row.** A house owned since 2000 was
27 rows, most of them "Ingen aktivitet". Shorter runs are left alone — folding two rows into one
saves nothing and adds something to click.

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
`/properties/:propertyId` (dashboard), `/properties/:propertyId/{valuations,projects,maintenance,budget,documents,components}`
(`components` is that property's **own** list — the central registry is under `/admin`),
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

**Client-side routes surviving a page refresh is `Program.cs`'s job**, and the rules are not
optional boilerplate. The App Service serves the SPA from `wwwroot`, so without a fallback it looks
for a physical file at e.g. `/properties/<guid>` and 404s; the routes only appear to work because
in-app navigation never asks the server for them. (This shipped broken once under the Static Web App
for the same reason, and was caught by refreshing a property page.) Three rules, and the two
exclusions matter as much as the rewrite:
- Anything else → `index.html`, so a deep link reloads.
- `/api/*` → **404**, never `index.html`. Handing HTML back from an API path turns "no such
  endpoint" into a JSON parse error somewhere far from the cause.
- Anything with a file extension → **404**, never `index.html`. A stale hashed bundle must fail as a
  missing script rather than as HTML served under a `.js` URL, which surfaces as a confusing
  MIME-type error instead of an obvious 404.

The fallback is registered **only when `wwwroot/index.html` exists**. Local `dotnet run` and the test
host have no SPA build and boot the same `Program`, so the guard is what keeps them unaffected.
`SpaFallbackTests` pins all of it — these rules previously lived in
`frontend/public/staticwebapp.config.json`, which is deleted.

### User suggestions are GitHub issues

`FeedbackController` files suggestions into `kallemk/houseapp` as issues and reads them back, so
there's no feedback store of the app's own.

**The `feedback` label is the whole safety mechanism, and it works by omission rather than by
filtering.** The app only ever *asks* GitHub for issues carrying that label, so ordinary development
issues aren't excluded further down — they're never fetched. A visibility condition somewhere in the
controller could be edited away without anyone noticing until private planning notes appeared in the
app; a query that never sees them can't. `FeedbackControllerTests` pins that an unlabelled issue is
invisible **even to an admin**.

Three labels form the contract:
- `feedback` — applied by the app to everything it creates. Nothing without it is ever read.
- `publik` — added by the owner in GitHub, and required before *other* users see a suggestion. Its
  submitter always sees their own (so they know it arrived), and admins see everything labelled
  `feedback` (so triage is possible from inside the app). The latest comment is shown as the reply,
  with its author and date — a bare quote left people wondering when it had been answered.
- `status:planerad` / `status:pågår` / `status:klar` / `status:avvisad` — optional, shown as a badge.
  Absent, the status falls back to the issue's open/closed state, so forgetting to label is harmless
  rather than misleading.

**Open/closed is reported separately from the status, and has to be.** A `status:*` label *overrides*
the derived status, so a closed issue labelled `status:pågår` would otherwise read as ongoing with no
sign it had been shut — which is exactly what made the model hard to follow before `IsOpen` was
surfaced. The two answer different questions: the label says what you decided, the state says whether
it's still being worked on. The page shows both badges and says so in a sentence.

**Ownership lives in the issue body, not a container.** A `<!-- houseapp:submitter={userId} -->`
marker is what makes "show me my own" and the daily submission cap work. That avoids a new Cosmos
container, which would have meant a Bicep change and the two-push sequencing that goes with it. The
marker is stripped before the body is shown back to the user.

**The submitter's display name and id go to GitHub; their email deliberately never does.** That's why
`CookiesPage` has a section about it — this is the one place user-entered data leaves Azure, and the
page would otherwise be wrong. Note that if the repository were ever made public, every suggestion in
it becomes public too.

The list is cached briefly (`Feedback:CacheSeconds`, default 120) because otherwise every page load
is a pair of calls against a rate-limited API; the tests set it to 0 so seeding is immediately
visible. `POST` is capped per user per day — anyone can sign up now, and this endpoint writes into a
private repo.

**The GitHub secret is `GH_ISSUES_TOKEN`, not `GITHUB_TOKEN`** — Actions reserves that name and
refuses to create a secret with it. The token should be a fine-grained PAT scoped to this repository
alone with Issues read/write. Issues are authored by the token's owner, which is why the body names
the real submitter.

### Footer, and the deliberate absence of a cookie banner

`components/layout/AppFooter.tsx` sits on the app shell, the property picker and the login page:
Odenbulten Consulting AB attribution, a link to the repo's `PITCH.md`, a link to `/cookies`, and the
build's short commit sha (injected as `VITE_APP_VERSION` from `github.sha` in `ci-cd.yml`, empty
locally). The sha is there so a bug report can name an exact build.

**There is no cookie consent banner, and that is a decision rather than an omission.** The app stores
exactly two things in the browser — the `houseapp.auth` session cookie and a `lastPropertyId` in
local storage — and both are strictly necessary for it to work at all. Necessary storage of that kind
doesn't require consent, so a banner would be asking permission for something the app cannot function
without, which mostly teaches people to dismiss consent dialogs unread. **If anything non-essential is
ever added — analytics, embedded video, advertising, any third-party pixel — that calculus changes and
a real consent mechanism becomes necessary.** `pages/CookiesPage.tsx` documents the current state and
is the place to notice; keep it accurate if the storage changes.

That page is on a **public route, outside `ProtectedRoute`**, because deciding whether to sign in is
exactly when someone wants to read what will be stored about them. It's written as a factual
description of how the app behaves, not as legal advice.

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
`appInsights` → `appService` (which joins the pre-existing shared plan). Notable non-obvious
choices, don't "fix" these without re-reading why:
- The App Service runs on a **shared B1 Linux plan that this deployment does not own**:
  `plan-common-001` in resource group `rg-common`, referenced with an `existing` resource so a
  deploy here can never modify it. Two consequences: the App Service takes its **region from the
  plan**, not from `location` (an app must sit in its plan's region), and the deploying service
  principal needs rights on `rg-common` to join it — a permission this deployment didn't need while
  it created its own plan.

  **An existing app can never be moved onto a plan in another resource group, and the app was
  therefore recreated rather than repointed.** Azure deploys each plan into a *webspace* keyed by
  resource group + region + OS, and
  [an app can only move between plans in the same webspace](https://learn.microsoft.com/en-us/azure/app-service/app-service-plan-manage#move-an-app-to-another-app-service-plan);
  plans can't change webspace afterwards, so moving the plan doesn't help either. Attempting it fails
  with a uselessly vague `Conflict / 59602: Cannot change the site ... due to hosting constraints`.
  Creating a *new* app against a cross-resource-group plan is a different operation and is fine —
  which is why dropping the `-api` suffix mattered for more than tidiness: the new name made the
  migration a create, letting the old app serve traffic until the new one was verified. B1 was chosen over Container Apps for deploy simplicity (plain
  `dotnet publish` + zip deploy, no Docker/registry). `alwaysOn: true` is the point of it.
- **There is no Static Web App any more, and the App Service serves the SPA itself.** This replaced
  an F1 App Service + a Standard-tier SWA whose only real job was proxying `/api/*` same-origin.
  Same-origin is now simply true rather than arranged, so the whole `linkedBackends` constraint that
  forced the SWA onto Standard is gone. History worth keeping, because it explains the shape the app
  had for most of its life: `linkedBackends` is Standard-only and fails with a misleading
  `SkuCode 'Free' is invalid`; `staticwebapp.config.json` can't stand in, because `rewrite` values
  [must be relative to the app root](https://learn.microsoft.com/en-us/azure/static-web-apps/configuration);
  and the reason "serve the SPA from the App Service" was rejected in July 2026 was that **F1 can't
  have a custom domain** — which stopped being true the moment the plan became B1. If you ever go
  back to a separate frontend host, `SameSite=None` + CORS is still the wrong answer: Safari blocks
  third-party cookies by default and iOS is how this app mostly gets used.
- Cosmos DB is **Serverless capacity mode**, not the "free tier" discount — avoids the
  one-per-subscription free-tier constraint and has no idle floor or cold-start/resume
  delay (unlike SQL Serverless-style auto-pause).
- **The custom domain and its certificate are deliberately not in Bicep.** Binding a hostname
  requires DNS to already resolve, and an App Service Managed Certificate requires the binding to
  already exist — a two-pass dependency that's easy to half-apply. `az webapp config hostname add`
  → `az webapp config ssl create` → `az webapp config ssl bind`, with `appBaseUrl` in Bicep naming
  the result so the Google Drive redirect URI matches.
- Storage CORS allows `*` for origins deliberately (see comment in
  `modules/storage.bicep`): security for direct browser-to-blob SAS uploads comes from the
  SAS signature/expiry, not origin restriction. It predates the App Service serving the SPA, when
  wiring the frontend's hostname back into the storage module would have created a circular module
  dependency; now that there is a single origin it could be tightened, but the security argument
  never depended on it.
- The two seed accounts are passed as `@secure()` object params (`seedUser1`/`seedUser2`)
  from GitHub Actions secrets — never put real values in `infra/main.parameters.json`
  (committed placeholders are `"REPLACE_ME"`).

## CI/CD

Two GitHub Actions workflows (`.github/workflows/`), using OIDC federated Azure login (no stored
client secret):
- `ci-cd.yml` — lint/build/test the frontend and backend; on push to `main`, build the SPA, copy it
  into the API's publish `wwwroot`, deploy both to App Service,
  **followed by an explicit `az webapp restart`**. That restart is load-bearing: zip deploy
  overwrites the DLLs in `/home/site/wwwroot` (an Azure Files mount) while the old process still
  has them memory-mapped, so the first method JIT'd after the swap reads the new file at stale
  offsets and throws `BadImageFormatException: Bad binary signature`. It surfaces as a 500 from
  whichever endpoint is called first after a deploy — with a stack that dies in
  `ObjectMethodExecutor..ctor` *before* the controller is even constructed, so it looks nothing
  like an application bug. Don't remove the restart step. (`WEBSITE_RUN_FROM_PACKAGE=1` would fix
  this more fundamentally, and would also stop zip deploy leaving deleted files behind in
  `wwwroot` — deliberately not adopted yet, the restart was the smaller change. Now that the SPA
  ships in `wwwroot` too, zip deploy also leaves old hashed bundles behind; harmless, since names are
  content-hashed, but it's a second argument for it.)

  **One workflow, because there is now one artifact.** The frontend and backend used to deploy
  separately, path-filtered; they can't any more without racing to deploy the same App Service. The
  cost is that every push redeploys both, and the SPA is briefly down during the restart — acceptable
  at this scale, and the client's retry (see `api/client.ts`) covers a reload that lands mid-deploy.
- `infra-deploy.yml` — `bicep build` + `what-if` always; `deployment group create` on push
  to `main`, gated behind the `production` GitHub Environment (manual approval required).
