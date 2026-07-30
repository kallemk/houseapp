# Google Drive as an alternative document store

> **Status: designed, not built.** Parked on 2026-07-30 to do smaller things first. Nothing in the
> codebase implements any of this yet — documents are still Blob-only. The decisions and the research
> behind them are settled, so picking this up should not need the investigation repeating.

## Context

Documents currently live in Azure Blob Storage. As the app spreads beyond one household, keeping
files somewhere people already look — their own Google Drive — lowers the barrier, and the couple's
Drive is where a lot of house paperwork already is.

The original idea was to paste a Drive folder URL onto the property and have the app do CRUD in it.
**That specific shape doesn't work**, and the research below is why the design differs.

### What the research established

- **A folder URL grants nothing.** It's just an id; sharing lives on the folder, and "anyone with
  the link" is a *Drive UI* permission. The Drive API always needs OAuth — there is no anonymous
  write.
- **The service-account shortcut is a dead end.** [Service accounts have no storage quota and can't
  own files](https://developers.google.com/workspace/drive/api/guides/handle-errors); they need a
  Shared Drive, which is Workspace-only. It would also have introduced the app's first stored
  long-lived secret, against a design where Cosmos and Storage both use managed identity.
- **`drive.file` is non-sensitive** ([scopes](https://developers.google.com/workspace/drive/api/guides/api-specific-auth)):
  basic verification, **no security assessment**. `drive`/`drive.readonly` are restricted and would
  require one — which is what pasting an arbitrary folder URL would have needed.
- **`drive.file` only reaches what the app created or the user picked.** Since the app creates the
  folder and every document goes through the app's own upload, that covers the whole lifecycle. A
  file dropped into the folder from Drive's web UI stays invisible to the app — accept this.
- **The consent screen must be published.** Testing status
  [expires refresh tokens after 7 days](https://developers.google.com/identity/protocols/oauth2),
  which is harmless today *only* because nothing uses refresh tokens. Publishing with non-sensitive
  scopes needs basic verification, not an assessment.

**Confirmed decisions:** full integration with `drive.file`; **the app creates the folder** on
connect (no Picker, no extra API key); **Blob Storage stays** alongside, per property, with the
choice of consolidating left for later.

## 1. Storage becomes a per-property choice

`IDocumentStorage` — a new interface over the two backends, with `BlobStorageService` becoming one
implementation and `GoogleDriveStorageService` the other. `Services/IBlobStorageService.cs` already
has the right shape (upload/download/delete); the split is by *property*, resolved per request.

`Document` gains:
- `StorageKind` enum — **`Blob = 0` first**, so the missing JSON property on every existing document
  reads as Blob and nothing needs migrating.
- `DriveFileId`, `DriveWebViewLink` (both nullable). `BlobPath` becomes nullable for Drive rows.

`Property` gains `GoogleDriveFolderId`, `GoogleDriveFolderUrl`, and `GoogleDriveConnectedByUserId`.

**One Drive connection per property, not per user** — forced by `drive.file`: each user only sees
files *they* created through the app, so two members uploading with their own tokens would each see
half the folder. Instead all uploads use the connecting user's token; everyone sees the same Cosmos
metadata, and `webViewLink` opens the file under Drive's own sharing. Consequence to handle
explicitly: if that person disconnects, uploads for the property fail — surface it as a clear
"Drive-anslutningen behöver förnyas" rather than a generic error.

## 2. OAuth

New `Controllers/DriveAuthController.cs`:
- `GET /api/drive/connect?propertyId=…` → redirect to Google with
  `scope=drive.file`, `access_type=offline`, `prompt=consent`, and a signed `state` carrying the
  property id and user id.
- `GET /api/drive/callback` → exchange the code, store the refresh token, create the folder, redirect
  back into the app.
- `DELETE /api/drive/connection?propertyId=…` → forget the token and the folder link (leaves the
  Drive folder and its files alone — the app never deletes a user's folder).

**The refresh token is encrypted at rest with `IDataProtector`** before going onto `ApplicationUser`.
Data Protection is already configured (`AddHouseAppDataProtection`, keys on the App Service's local
disk), so this reuses the existing key ring rather than inventing key management. Note the coupling:
losing that key ring already means everyone signs in again; it would now also mean reconnecting
Drive.

**The callback must be `https://housetracker.odenbulten.se/api/drive/callback`** so it proxies
through the Static Web App to the App Service. A callback pointing at the App Service hostname is the
same trap that made a redirect flow wrong for sign-in.

New config `Authentication:Google:ClientSecret` — **the app's first real secret**. `@secure()` Bicep
param fed from a new `GOOGLE_CLIENT_SECRET` GitHub secret, surfaced as an app setting exactly like
the seed users. It must never join `GOOGLE_CLIENT_ID` as a plain repo *variable*.

## 3. Uploads change shape for Drive

Blob uploads go browser → Blob via a SAS URL, never through the API. Drive can't work that way
without putting a Drive token in the browser, so **Drive uploads POST the file to the API**, which
streams it to Drive and writes the metadata row.

That reverses a property CLAUDE.md calls out ("documents never pass through the API"), so it needs
saying plainly: on F1 App Service this shares a 60 CPU-min/day quota and a request size limit. Fine
for the PDFs and photos this app handles; worth a `RequestSizeLimit` and a clear error above it.

`DocumentsController` keeps its existing endpoints for Blob and gains a multipart `POST
/api/documents/upload` used when the property is on Drive. Every action keeps its
`CanAccessPropertyAsync` guard.

## 4. Frontend

- **`PropertyPickerPage`** card menu (members only): "Anslut Google Drive" / "Koppla från Google
  Drive", plus a link to the folder once connected.
- **`FileUpload`** already owns the upload interaction; it branches on the property's storage kind —
  SAS PUT as today, or multipart POST to the API for Drive. `DocumentsPage`, `ProjectDocuments` and
  `QuickAddModal` all go through it, so none of them change.
- **`DocumentsPage`** opens Drive documents via `webViewLink` in a new tab instead of a SAS URL.
- Swedish copy for the connect flow and for the "connection needs renewing" case.

## 5. Tests

`GoogleDriveStorageService` sits behind `IDocumentStorage`, so it gets a `FakeDriveStorage` in
`HouseAppWebApplicationFactory` exactly as `FakeBlobStorageService` and `FakeGoogleTokenValidator`
already do — the whole document path stays under test without touching Google.

- Documents on a Blob property keep working unchanged (the regression that matters most).
- A property with a Drive connection routes uploads to the Drive backend and stores `DriveFileId`.
- `StorageKind` absent on an existing document reads as Blob.
- Deleting a property deletes its Drive documents' metadata; whether it deletes the Drive **files**
  is a decision to make explicitly — default to leaving them, since they're in the user's own Drive.
- Refresh token round-trips through the protector and is never returned by any DTO.
- Access guards still apply to the new upload endpoint and the connect endpoints.

## 6. Docs

`CLAUDE.md` states documents never pass through the API and that there is no client secret anywhere —
both change. Record: why `drive.file` and not `drive`; why the app creates the folder rather than
accepting a URL; why one connection per property; that the consent screen must stay **published** or
refresh tokens die after 7 days. Update `README.md`'s Google setup with the new scope, secret and
callback URL.

## What you need to do in Google Cloud Console

1. Add `https://housetracker.odenbulten.se/api/drive/callback` under **Authorized redirect URIs** —
   currently empty, because the ID-token flow never redirects.
2. Enable the **Google Drive API** for the project.
3. Add the `.../auth/drive.file` scope on the consent screen.
4. **Publish the app** (Testing → In production). Non-sensitive scopes only, so this is basic
   verification rather than a security assessment — but it is not instant, and until it's done
   refresh tokens last 7 days.
5. Create a **client secret** for the existing OAuth client and put it in the `GOOGLE_CLIENT_SECRET`
   GitHub secret.

## Verification

1. `cd backend && dotnet test`; `cd frontend && npm run build && npm run lint`;
   `az bicep build --file infra/main.bicep`.
2. Locally: connect Drive on a test property, confirm the folder appears in Drive, upload a document,
   confirm it lands in that folder and opens from the documents page, then delete it and confirm it
   leaves Drive.
3. Confirm a Blob-backed property is completely unaffected — upload, download and delete still work
   via SAS, and existing documents still open.
4. Disconnect and confirm the app says the connection is gone rather than failing obscurely, and that
   the Drive folder and its files survive.
5. After deploy, re-run the Playwright walk to check the documents page for both a Blob property and
   a Drive one.

## Sequencing

The Bicep change (the `@secure()` client-secret param) must deploy **before** the backend that reads
it, same two-push rule as the container changes. The Google Console steps above should be done first
of all — publishing is the long pole, and nothing works end-to-end until the redirect URI exists.
