import { apiClient } from './client'
import type { DocumentCategory, DocumentDto } from './types'

interface UploadUrlResponse {
  /** Sas = PUT straight to Blob Storage; Drive = post the file to the API instead. */
  mode: 'Sas' | 'Drive'
  uploadUrl: string | null
  blobPath: string | null
}

interface DownloadUrlResponse {
  downloadUrl: string
}

export const documentsApi = {
  listForProperty: (propertyId: string) => apiClient.get<DocumentDto[]>(`/properties/${propertyId}/documents`),

  /**
   * The server decides which backend this property uses, so the client always asks first rather than
   * reading it off a possibly-stale property in the cache. Blob keeps the direct-to-storage SAS
   * upload; Drive can't have one without handing the browser a Drive token, so the file goes through
   * the API.
   */
  async upload(
    propertyId: string,
    file: File,
    category: DocumentCategory,
    date: string,
    title: string | null,
    projectId?: string | null,
  ): Promise<DocumentDto> {
    const contentType = file.type || 'application/octet-stream'
    const { mode, uploadUrl, blobPath } = await apiClient.post<UploadUrlResponse>('/documents/upload-url', {
      propertyId,
      fileName: file.name,
      contentType,
    })

    if (mode === 'Drive') {
      const form = new FormData()
      form.append('propertyId', propertyId)
      if (projectId) {
        form.append('projectId', projectId)
      }
      form.append('date', date)
      form.append('title', title ?? '')
      form.append('category', category)
      form.append('file', file)
      return apiClient.postForm<DocumentDto>('/documents/upload', form)
    }

    const putResponse = await fetch(uploadUrl!, {
      method: 'PUT',
      headers: {
        'x-ms-blob-type': 'BlockBlob',
        'Content-Type': contentType,
      },
      body: file,
    })
    if (!putResponse.ok) {
      throw new Error(`Upload to storage failed: ${putResponse.statusText}`)
    }

    return apiClient.post<DocumentDto>('/documents', {
      propertyId,
      // Was still named renovationEntryId after the project rename, so it never bound to the
      // renamed ProjectId and every attachment was silently dropped.
      projectId: projectId ?? null,
      date,
      title,
      fileName: file.name,
      contentType,
      blobPath,
      sizeBytes: file.size,
      category,
    })
  },

  /**
   * Drive documents carry their own link, stored at upload — no round trip and no live connection
   * needed to open one. Blob documents need a freshly signed SAS URL every time.
   */
  async download(document: DocumentDto) {
    const url =
      document.driveWebViewLink ??
      (
        await apiClient.get<DownloadUrlResponse>(
          `/documents/${document.id}/download-url?propertyId=${encodeURIComponent(document.propertyId)}`,
        )
      ).downloadUrl
    window.open(url, '_blank')
  },

  /** Attaches an existing document to a project, or detaches it with null. */
  setProject: (id: string, propertyId: string, projectId: string | null) =>
    apiClient.put<void>(`/documents/${id}/project?propertyId=${encodeURIComponent(propertyId)}`, { projectId }),

  /**
   * `deleteFromDrive` is opt-in per deletion — the file is in someone's personal Drive, so removing
   * it is their call each time rather than a side effect of tidying up the app. Blob files are always
   * deleted; nothing else could ever reach them.
   */
  remove: (id: string, propertyId: string, deleteFromDrive = false) =>
    apiClient.delete<void>(
      `/documents/${id}?propertyId=${encodeURIComponent(propertyId)}&deleteFromDrive=${deleteFromDrive}`,
    ),
}

export const driveApi = {
  /**
   * A full-page navigation, not a fetch: this ends at Google's consent screen and comes back as a
   * redirect. It also works *because* it's top-level — SameSite=Lax sends the session cookie on a
   * top-level GET but not on a cross-site fetch.
   */
  connect(propertyId: string) {
    window.location.href = `/api/drive/connect?propertyId=${encodeURIComponent(propertyId)}`
  },

  disconnect: (propertyId: string) =>
    apiClient.delete<void>(`/drive/connection?propertyId=${encodeURIComponent(propertyId)}`),
}
