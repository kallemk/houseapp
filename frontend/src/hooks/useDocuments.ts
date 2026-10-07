import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { isDriveConnectionExpired } from '../api/client'
import { documentsApi, driveApi } from '../api/documents'
import type { DocumentCategory } from '../api/types'

const key = (propertyId: string) => ['documents', propertyId]
const driveStatusKey = (propertyId: string) => ['driveStatus', propertyId]

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
    // The status may have been cached as Ok before the grant lapsed; let the card catch up.
    onError: (error) => {
      if (isDriveConnectionExpired(error)) {
        queryClient.invalidateQueries({ queryKey: driveStatusKey(propertyId) })
      }
    },
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
      queryClient.invalidateQueries({ queryKey: driveStatusKey(propertyId) })
    },
  })
}

/**
 * Whether the property's Drive grant still works. Costs the backend a token refresh against Google,
 * so it's only asked for properties actually on Drive, and not on every render.
 */
export function useDriveStatus(propertyId: string, enabled: boolean) {
  return useQuery({
    queryKey: driveStatusKey(propertyId),
    queryFn: () => driveApi.status(propertyId),
    enabled: enabled && !!propertyId,
    staleTime: 5 * 60 * 1000,
  })
}
