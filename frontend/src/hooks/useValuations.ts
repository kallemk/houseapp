import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { valuationsApi, type ValuationEntryInput } from '../api/valuations'

const key = (propertyId: string) => ['valuations', propertyId]

export function useValuations(propertyId: string) {
  return useQuery({
    queryKey: key(propertyId),
    queryFn: () => valuationsApi.listForProperty(propertyId),
    enabled: !!propertyId,
  })
}

export function useCreateValuation(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: ValuationEntryInput) => valuationsApi.create(propertyId, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}

export function useUpdateValuation(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: ValuationEntryInput }) =>
      valuationsApi.update(id, propertyId, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}

export function useDeleteValuation(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => valuationsApi.remove(id, propertyId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}
