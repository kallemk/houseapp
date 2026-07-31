import { apiClient } from './client'
import type { MeResponse } from './types'

// Signing in is the request most likely to hit a sleeping backend — it's usually the first thing
// anyone does — and it's safe to repeat, so both paths opt into the retry that POSTs skip by default.
const RETRY = { retryWhileWaking: true }

export const authApi = {
  login: (email: string, password: string) =>
    apiClient.post<MeResponse>('/auth/login', { email, password }, RETRY),
  /** `credential` is the ID token from Google Identity Services. */
  loginWithGoogle: (credential: string) =>
    apiClient.post<MeResponse>('/auth/google', { credential }, RETRY),
  logout: () => apiClient.post<void>('/auth/logout'),
  me: () => apiClient.get<MeResponse>('/auth/me'),
  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.post<void>('/auth/change-password', { currentPassword, newPassword }),
}
