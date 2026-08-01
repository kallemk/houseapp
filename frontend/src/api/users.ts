import { apiClient } from './client'
import type { UserDto } from './types'

export interface CreateUserInput {
  email: string
  displayName: string
  /** Blank/undefined means the user can only sign in with Google. */
  initialPassword?: string | null
}

export interface UpdateUserInput {
  displayName: string
  isAdmin: boolean
  isBlocked: boolean
}

export const usersApi = {
  list: () => apiClient.get<UserDto[]>('/users'),
  create: (input: CreateUserInput) => apiClient.post<UserDto>('/users', input),
  update: (id: string, input: UpdateUserInput) => apiClient.put<void>(`/users/${id}`, input),
  remove: (id: string) => apiClient.delete<void>(`/users/${id}`),
}
