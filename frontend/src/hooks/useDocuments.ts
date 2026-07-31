import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { documentsApi, driveApi } from '../api/documents'
import type { DocumentCategory } from '../api/types'

const key = (propertyId: string) => ['documents', propertyId]

export function useDocuments(propertyId: string) {
  return useQuery({
    queryKey: key(propertyId),
    queryFn: () => documentsApi.listForProperty(propertyId),
    enabled: !!propertyId,
  })
}

export function useUploadDocument(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      file,
      category,
      date,
      title,
      projectId,
    }: {
      file: File
      category: DocumentCategory
      date: string
      title: string | null
      projectId?: string | null
    }) => documentsApi.upload(propertyId, file, category, date, title, projectId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}

export function useUpdateDocument(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      id,
      ...input
    }: {
      id: string
      title: string
      date: string
      category: DocumentCategory
    }) => documentsApi.update(id, propertyId, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}

export function useSetDocumentProject(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, projectId }: { id: string; projectId: string | null }) =>
      documentsApi.setProject(id, propertyId, projectId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}

export function useDeleteDocument(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, deleteFromDrive }: { id: string; deleteFromDrive?: boolean }) =>
      documentsApi.remove(id, propertyId, deleteFromDrive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}

/**
 * Connecting or disconnecting changes where uploads go and what the property looks like, so both the
 * property list and this property's documents are stale afterwards.
 */
export function useDisconnectDrive(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => driveApi.disconnect(propertyId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['properties'] })
      queryClient.invalidateQueries({ queryKey: key(propertyId) })
    },
  })
}
