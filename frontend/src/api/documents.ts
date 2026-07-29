import { apiClient } from './client'
import type { DocumentCategory, DocumentDto } from './types'

interface UploadUrlResponse {
  uploadUrl: string
  blobPath: string
}

interface DownloadUrlResponse {
  downloadUrl: string
}

export const documentsApi = {
  listForProperty: (propertyId: string) => apiClient.get<DocumentDto[]>(`/properties/${propertyId}/documents`),

  /** Uploads the file straight to Blob Storage via a SAS URL, then saves its metadata. */
  async upload(
    propertyId: string,
    file: File,
    category: DocumentCategory,
    date: string,
    projectId?: string | null,
  ): Promise<DocumentDto> {
    const { uploadUrl, blobPath } = await apiClient.post<UploadUrlResponse>('/documents/upload-url', {
      propertyId,
      fileName: file.name,
      contentType: file.type || 'application/octet-stream',
    })

    const putResponse = await fetch(uploadUrl, {
      method: 'PUT',
      headers: {
        'x-ms-blob-type': 'BlockBlob',
        'Content-Type': file.type || 'application/octet-stream',
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
      fileName: file.name,
      contentType: file.type || 'application/octet-stream',
      blobPath,
      sizeBytes: file.size,
      category,
    })
  },

  async download(id: string, propertyId: string) {
    const { downloadUrl } = await apiClient.get<DownloadUrlResponse>(
      `/documents/${id}/download-url?propertyId=${encodeURIComponent(propertyId)}`,
    )
    window.open(downloadUrl, '_blank')
  },

  /** Attaches an existing document to a project, or detaches it with null. */
  setProject: (id: string, propertyId: string, projectId: string | null) =>
    apiClient.put<void>(`/documents/${id}/project?propertyId=${encodeURIComponent(propertyId)}`, { projectId }),

  remove: (id: string, propertyId: string) =>
    apiClient.delete<void>(`/documents/${id}?propertyId=${encodeURIComponent(propertyId)}`),
}
