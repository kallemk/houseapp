import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { renovationTypesApi, type RenovationTypeInput } from '../api/renovationTypes'

const KEY = ['renovation-types']

export function useRenovationTypes() {
  return useQuery({ queryKey: KEY, queryFn: renovationTypesApi.list })
}

export function useCreateRenovationType() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: RenovationTypeInput) => renovationTypesApi.create(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useUpdateRenovationType() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: RenovationTypeInput }) => renovationTypesApi.update(id, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useDeleteRenovationType() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => renovationTypesApi.remove(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}
