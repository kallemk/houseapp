import { apiClient } from './client'
import type { ValuationEntryDto } from './types'

export interface ValuationEntryInput {
  date: string
  value: number
  source?: string | null
  notes?: string | null
}

export const valuationsApi = {
  listForProperty: (propertyId: string) => apiClient.get<ValuationEntryDto[]>(`/properties/${propertyId}/valuations`),
  create: (propertyId: string, input: ValuationEntryInput) =>
    apiClient.post<ValuationEntryDto>(`/properties/${propertyId}/valuations`, input),
  update: (id: string, propertyId: string, input: ValuationEntryInput) =>
    apiClient.put<void>(`/valuations/${id}?propertyId=${encodeURIComponent(propertyId)}`, input),
  remove: (id: string, propertyId: string) =>
    apiClient.delete<void>(`/valuations/${id}?propertyId=${encodeURIComponent(propertyId)}`),
}
