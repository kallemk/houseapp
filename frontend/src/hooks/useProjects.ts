import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { projectsApi, type SaveProjectInput } from '../api/projects'

const listKey = (propertyId: string) => ['projects', propertyId]
const itemKey = (propertyId: string, id: string) => ['projects', propertyId, id]

export function useProjects(propertyId: string) {
  return useQuery({
    queryKey: listKey(propertyId),
    queryFn: () => projectsApi.listForProperty(propertyId),
    enabled: !!propertyId,
  })
}

export function useProject(propertyId: string, id: string) {
  return useQuery({
    queryKey: itemKey(propertyId, id),
    queryFn: () => projectsApi.getById(id, propertyId),
    enabled: !!propertyId && !!id,
  })
}

export function useCreateProject(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: SaveProjectInput) => projectsApi.create(propertyId, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: listKey(propertyId) }),
  })
}

export function useUpdateProject(propertyId: string, id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: SaveProjectInput) => projectsApi.update(id, propertyId, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: listKey(propertyId) })
      queryClient.invalidateQueries({ queryKey: itemKey(propertyId, id) })
    },
  })
}

export function useDeleteProject(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => projectsApi.remove(id, propertyId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: listKey(propertyId) }),
  })
}
