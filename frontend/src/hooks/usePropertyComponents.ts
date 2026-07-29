import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { propertyComponentsApi, type PropertyComponentInput } from '../api/propertyComponents'

const KEY = ['property-components']

export function usePropertyComponents() {
  return useQuery({ queryKey: KEY, queryFn: propertyComponentsApi.list })
}

export function useCreatePropertyComponent() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: PropertyComponentInput) => propertyComponentsApi.create(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useUpdatePropertyComponent() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: PropertyComponentInput }) =>
      propertyComponentsApi.update(id, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useDeletePropertyComponent() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => propertyComponentsApi.remove(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}
