import { apiClient } from './client'
import type { PropertyComponentDto } from './types'

export interface PropertyComponentInput {
  name: string
  recommendedIntervalMonths: number | null
}

export const propertyComponentsApi = {
  list: () => apiClient.get<PropertyComponentDto[]>('/property-components'),
  create: (input: PropertyComponentInput) => apiClient.post<PropertyComponentDto>('/property-components', input),
  update: (id: string, input: PropertyComponentInput) => apiClient.put<void>(`/property-components/${id}`, input),
  remove: (id: string) => apiClient.delete<void>(`/property-components/${id}`),
}
