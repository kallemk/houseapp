import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { renovationsApi, type RenovationEntryInput } from '../api/renovations'

const key = (propertyId: string) => ['renovation-entries', propertyId]

export function useRenovationEntries(propertyId: string) {
  return useQuery({
    queryKey: key(propertyId),
    queryFn: () => renovationsApi.listForProperty(propertyId),
    enabled: !!propertyId,
  })
}

export function useCreateRenovationEntry(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: RenovationEntryInput) => renovationsApi.create(propertyId, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}

export function useDeleteRenovationEntry(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => renovationsApi.remove(id, propertyId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}
