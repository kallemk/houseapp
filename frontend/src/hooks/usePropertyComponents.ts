import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  propertyComponentSetApi,
  propertyComponentsApi,
  type PropertyComponentInput,
} from '../api/propertyComponents'

const KEY = ['property-components']

const setKey = (propertyId: string) => ['properties', propertyId, 'components']

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

// --- One property's own component set --------------------------------------------------------

/**
 * The components in force for a property — its own list once customised, otherwise the central
 * registry. Everything property-scoped (the projects dropdown, the quick-add modal, the components
 * page) reads this rather than the central list, so a locally renamed component shows its local
 * name everywhere.
 */
export function usePropertyComponentSet(propertyId: string) {
  return useQuery({
    queryKey: setKey(propertyId),
    queryFn: () => propertyComponentSetApi.get(propertyId),
    enabled: !!propertyId,
  })
}

/** Convenience for the callers that only want the list, in the shape the central list had. */
export function usePropertyComponentList(propertyId: string) {
  const query = usePropertyComponentSet(propertyId)
  return { ...query, data: query.data?.components }
}

/** Intervals and names feed the maintenance schedule, so it goes stale with every change here. */
function useComponentSetMutation<TVariables>(
  propertyId: string,
  mutationFn: (variables: TVariables) => Promise<unknown>,
) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: setKey(propertyId) })
      queryClient.invalidateQueries({ queryKey: ['maintenance-schedule', propertyId] })
    },
  })
}

export function useCreateLocalComponent(propertyId: string) {
  return useComponentSetMutation(propertyId, (input: PropertyComponentInput) =>
    propertyComponentSetApi.create(propertyId, input),
  )
}

export function useUpdateLocalComponent(propertyId: string) {
  return useComponentSetMutation(propertyId, ({ id, input }: { id: string; input: PropertyComponentInput }) =>
    propertyComponentSetApi.update(propertyId, id, input),
  )
}

export function useDeleteLocalComponent(propertyId: string) {
  return useComponentSetMutation(propertyId, (id: string) => propertyComponentSetApi.remove(propertyId, id))
}

export function useSyncComponentsFromCentral(propertyId: string) {
  return useComponentSetMutation(propertyId, () => propertyComponentSetApi.sync(propertyId))
}
