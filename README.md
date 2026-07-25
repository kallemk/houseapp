# houseapp

A private app for tracking our house: value over time, renovation/investment spend, and documents/photos.

## Structure

- `backend/` — ASP.NET Core Web API (.NET 9), Cosmos DB (EF Core), cookie auth
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
4. Seed accounts: on first run the API creates the two accounts defined in
   `backend/src/HouseApp.Api/appsettings.Development.json` under `Seed:Users`
   (email + temp password). Log in and change the password via
   `POST /api/auth/change-password`.

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
     for the two accounts. Never commit real values of these into
     `infra/main.parameters.json`.
3. **GitHub environment**: create a `production` environment with required
   reviewers, so infra and deploy jobs pause for manual approval.
4. Push to `main` (or run `infra-deploy` manually first) to provision
   everything, then the backend/frontend workflows can deploy on top of it.
