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
    renovationEntryId?: string | null,
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
      renovationEntryId: renovationEntryId ?? null,
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

  remove: (id: string, propertyId: string) =>
    apiClient.delete<void>(`/documents/${id}?propertyId=${encodeURIComponent(propertyId)}`),
}
