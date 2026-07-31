# houseapp

A private app for tracking our house: value over time, a log of projects (underhåll, renovering
och nyinvestering) with costs and contractors, a derived underhållsplan, yearly budgets, and
documents/photos.

## Structure

- `backend/` — ASP.NET Core Web API (.NET 9), Cosmos DB (EF Core), cookie auth
  (email+password or Sign in with Google)
- `frontend/` — React + TypeScript (Vite), Mantine, TanStack Query
- `infra/` — Bicep templates for the Azure deployment
- `docker-compose.yml` — local Cosmos DB Emulator + Azurite for offline development

## Local development

1. Start local dependencies:
   ```
   docker compose up -d
   ```
2. Run the backend:
   ```
   cd backend/src/HouseApp.Api
   dotnet run --launch-profile https
   ```
   Swagger UI is available at `https://localhost:7275/swagger` in Development.
3. Run the frontend (proxies `/api` to the backend above — see `vite.config.ts`):
   ```
   cd frontend
   npm install
   npm run dev
   ```
4. Seed accounts: on first run the API creates the bootstrap accounts defined in
   `backend/src/HouseApp.Api/appsettings.Development.json` under `Seed:Users`
   (email + temp password). Log in and change the password via
   `POST /api/auth/change-password`. Everyone else is added in-app on the
   **Användare** page. Seed accounts are admins; everyone added afterwards is a
   regular user until an admin ticks their **Admin** switch.
5. Properties are private to their members. Creating one connects only you; you
   share it from **Hantera åtkomst** on the property card, searching by name or
   email. Everyone additionally sees the shared **Demo** property, a sandbox any
   signed-in user can edit — an admin marks one property as the demo.
6. Google sign-in is optional locally — leave `Authentication:Google:ClientId`
   blank in `appsettings.Development.json` and the Google button simply doesn't
   render, leaving password login. To try it locally, set that value plus
   `VITE_GOOGLE_CLIENT_ID` in `frontend/.env.development`, and add
   `http://localhost:5173` as an authorized JavaScript origin (see below).

## Sign in with Google

Users authenticate with Google Identity Services: the browser obtains an ID token
and posts it to `POST /api/auth/google`, which verifies it and issues the app's
normal session cookie. There is **no client secret** and no OAuth redirect — only
a client ID, which is public because it ships inside the frontend bundle.

Only people who already exist in the `users` container may sign in; an
unrecognised Google account gets a 403. Manage that list on the **Användare**
page (it is also where you revoke access, by removing someone). That page is
admin-only, as is editing the list of property components.

One-time Google Cloud Console setup:

1. Create a project at <https://console.cloud.google.com/>.
2. **APIs & Services → OAuth consent screen**: User type **External**; fill in the
   app name and contact emails; keep the default non-sensitive scopes
   (`openid`, `email`, `profile`); add each person's Gmail address under
   **Test users**. Publishing status **Testing** is fine for sign-in alone — but
   see the Drive section below, which needs the app published.
3. **APIs & Services → Credentials → Create credentials → OAuth client ID**,
   type **Web application**. Under **Authorized JavaScript origins** add the
   Static Web App URL and `http://localhost:5173`. Redirect URIs are only needed
   for Google Drive (below) — sign-in never redirects.
4. Copy the client ID into the `GOOGLE_CLIENT_ID` GitHub repo *variable* (see
   below). It feeds both the frontend build and the backend app setting.

## Documents in Google Drive

Each property stores its documents in Azure Blob Storage by default. A member can
instead connect their **Google Drive**, and the app creates a folder in it and
uploads there from then on. It's per property, opt-in, and reversible — existing
documents keep working either way.

Unlike sign-in, this is a real OAuth redirect flow and **does use a client
secret**. Additional Console setup, on the same OAuth client:

1. **Authorized redirect URIs** — add both
   `https://<your-domain>/api/drive/callback` and
   `http://localhost:5173/api/drive/callback`. The local one is the *Vite dev
   server's* origin, not `https://localhost:7275`: Google redirects the browser,
   and it has to come back through the same proxy the app is served from.
2. **APIs & Services → Library** — enable the **Google Drive API**.
3. **OAuth consent screen → Scopes** — add `.../auth/drive.file`. It's
   non-sensitive, so this needs basic verification, not a security assessment.
   (`drive` and `drive.readonly` are restricted and would need one — which is why
   the app creates its own folder instead of accepting a folder URL.)
4. **Publish the app** (Testing → In production). While it's in Testing, refresh
   tokens expire after 7 days and every property has to be reconnected weekly.
   This no longer only affects sign-in, which never used refresh tokens.
5. **Create a client secret** on the OAuth client and put it in the
   `GOOGLE_CLIENT_SECRET` GitHub **secret** (not a variable — unlike the client
   ID, this one is a real credential).

Locally, set `Authentication:Google:ClientSecret` in
`appsettings.Development.json`; `DriveRedirectUri` is already pointed at the Vite
dev server there. Leave the secret blank and the Drive endpoints return 503,
which is the right behaviour on a machine that isn't set up for it.

## Deployment

Infra is defined in `infra/` (Bicep) and deployed via the `infra-deploy` GitHub
Actions workflow. The backend and frontend each have their own CI/CD workflow
that builds and deploys on push to `main`. See `.github/workflows/` for details.

### One-time setup

1. **Azure AD app registration for OIDC**: create an app registration with a
   federated credential trusting this GitHub repo (no client secret needed).
   Grant it `Contributor` on the target resource group.
2. **GitHub repo secrets** (Settings → Secrets and variables → Actions):
   - `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` — from the
     app registration above.
   - `AZURE_RESOURCE_GROUP` — the resource group to deploy into.
   - `AZURE_APP_SERVICE_NAME` — the App Service name (from the infra deploy's
     `appServiceName` output, once the first infra deploy has run).
   - `AZURE_STATIC_WEB_APPS_API_TOKEN` — from the Static Web App resource's
     deployment token (`az staticwebapp secrets list`), once the first infra
     deploy has run.
   - `SEED_USER_1`, `SEED_USER_2` — JSON strings like
     `{"email":"you@example.com","displayName":"You","tempPassword":"..."}`
     for the bootstrap accounts. Never commit real values of these into
     `infra/main.parameters.json`.
   - `GOOGLE_CLIENT_SECRET` — only needed for the Google Drive integration.
     Sign-in works without it; connecting Drive returns 503.
3. **GitHub repo variables** (same page, *Variables* tab — not secrets):
   - `GOOGLE_CLIENT_ID` — the OAuth client ID from the Google setup above. It's a
     *variable* rather than a secret because it's public by design, and it's read
     by both `frontend-ci-cd` (inlined into the bundle at build time) and
     `infra-deploy` (passed as the `googleClientId` Bicep param).
4. **GitHub environment**: create a `production` environment with required
   reviewers, so infra and deploy jobs pause for manual approval.
5. Push to `main` (or run `infra-deploy` manually first) to provision
   everything, then the backend/frontend workflows can deploy on top of it.
