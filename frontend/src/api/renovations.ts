import { apiClient } from './client'
import type { RenovationCategory, RenovationEntryDto } from './types'

export interface RenovationEntryInput {
  date: string
  category: RenovationCategory
  title: string
  description?: string | null
  amount: number
  vendor?: string | null
}

export const renovationsApi = {
  listForProperty: (propertyId: string) =>
    apiClient.get<RenovationEntryDto[]>(`/properties/${propertyId}/renovation-entries`),
  create: (propertyId: string, input: RenovationEntryInput) =>
    apiClient.post<RenovationEntryDto>(`/properties/${propertyId}/renovation-entries`, input),
  update: (id: string, propertyId: string, input: RenovationEntryInput) =>
    apiClient.put<void>(`/renovation-entries/${id}?propertyId=${encodeURIComponent(propertyId)}`, input),
  remove: (id: string, propertyId: string) =>
    apiClient.delete<void>(`/renovation-entries/${id}?propertyId=${encodeURIComponent(propertyId)}`),
}
