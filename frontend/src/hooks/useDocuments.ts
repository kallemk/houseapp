import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { documentsApi } from '../api/documents'
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
    mutationFn: (id: string) => documentsApi.remove(id, propertyId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}
