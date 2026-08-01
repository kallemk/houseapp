import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { usersApi, type CreateUserInput, type UpdateUserInput } from '../api/users'

const KEY = ['users']

/** `enabled: false` for a non-admin, who would only get a 403 from the endpoint. */
export function useUsers(enabled = true) {
  return useQuery({ queryKey: KEY, queryFn: usersApi.list, enabled })
}

export function useCreateUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: CreateUserInput) => usersApi.create(input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: KEY })
    },
  })
}

export function useUpdateUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateUserInput }) => usersApi.update(id, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useDeleteUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => usersApi.remove(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}
