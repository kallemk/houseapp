import { apiClient } from './client'
import type { MeResponse } from './types'

export const authApi = {
  login: (email: string, password: string) => apiClient.post<MeResponse>('/auth/login', { email, password }),
  /** `credential` is the ID token from Google Identity Services. */
  loginWithGoogle: (credential: string) => apiClient.post<MeResponse>('/auth/google', { credential }),
  logout: () => apiClient.post<void>('/auth/logout'),
  me: () => apiClient.get<MeResponse>('/auth/me'),
  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.post<void>('/auth/change-password', { currentPassword, newPassword }),
}
