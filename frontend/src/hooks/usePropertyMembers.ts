import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { propertyMembersApi } from '../api/propertyMembers'

const key = (propertyId: string) => ['property-members', propertyId]

export function usePropertyMembers(propertyId: string, enabled = true) {
  return useQuery({
    queryKey: key(propertyId),
    queryFn: () => propertyMembersApi.list(propertyId),
    enabled: enabled && !!propertyId,
  })
}

/** Debounced by the caller; below two characters this isn't sent at all. */
export function useMemberSearch(propertyId: string, query: string) {
  return useQuery({
    queryKey: ['property-member-candidates', propertyId, query],
    queryFn: () => propertyMembersApi.search(propertyId, query),
    enabled: !!propertyId && query.trim().length >= 2,
  })
}

export function useAddPropertyMember(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) => propertyMembersApi.add(propertyId, userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}

export function useRemovePropertyMember(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) => propertyMembersApi.remove(propertyId, userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: key(propertyId) })
      // Removing yourself drops the property out of your own list.
      queryClient.invalidateQueries({ queryKey: ['properties'] })
    },
  })
}

export function useSetDemoProperty() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ propertyId, isDemo }: { propertyId: string; isDemo: boolean }) =>
      propertyMembersApi.setDemo(propertyId, isDemo),
    // Setting one demo clears any other, so the whole list is stale.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['properties'] }),
  })
}
