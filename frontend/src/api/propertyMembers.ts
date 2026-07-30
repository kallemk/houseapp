import { apiClient } from './client'
import type { PropertyMemberDto } from './types'

export const propertyMembersApi = {
  list: (propertyId: string) => apiClient.get<PropertyMemberDto[]>(`/properties/${propertyId}/members`),

  /** Returns nothing below two characters — the server enforces the same floor. */
  search: (propertyId: string, query: string) =>
    apiClient.get<PropertyMemberDto[]>(
      `/properties/${propertyId}/member-candidates?query=${encodeURIComponent(query)}`,
    ),

  add: (propertyId: string, userId: string) =>
    apiClient.post<void>(`/properties/${propertyId}/members`, { userId }),

  remove: (propertyId: string, userId: string) =>
    apiClient.delete<void>(`/properties/${propertyId}/members/${userId}`),

  /** Admin-only. Marking one property as the demo clears the flag on any other. */
  setDemo: (propertyId: string, isDemo: boolean) =>
    apiClient.put<void>(`/properties/${propertyId}/demo`, { isDemo }),
}
