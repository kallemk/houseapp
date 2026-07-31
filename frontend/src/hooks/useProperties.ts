import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { propertiesApi, type CreatePropertyInput } from '../api/properties'

const KEY = ['properties']

export function useProperties() {
  return useQuery({ queryKey: KEY, queryFn: propertiesApi.list })
}

export function useCreateProperty() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: CreatePropertyInput) => propertiesApi.create(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useUpdateProperty() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: CreatePropertyInput }) => propertiesApi.update(id, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useDeleteProperty() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, deleteFromDrive }: { id: string; deleteFromDrive?: boolean }) =>
      propertiesApi.remove(id, deleteFromDrive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}
