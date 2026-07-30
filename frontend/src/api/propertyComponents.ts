import { apiClient } from './client'
import type { PropertyComponentDto, PropertyComponentSetDto, PropertyLocalComponentDto } from './types'

export interface PropertyComponentInput {
  name: string
  recommendedIntervalMonths: number | null
}

/** The shared registry every property starts from. Admin-only to change. */
export const propertyComponentsApi = {
  list: () => apiClient.get<PropertyComponentDto[]>('/property-components'),
  create: (input: PropertyComponentInput) => apiClient.post<PropertyComponentDto>('/property-components', input),
  update: (id: string, input: PropertyComponentInput) => apiClient.put<void>(`/property-components/${id}`, input),
  remove: (id: string) => apiClient.delete<void>(`/property-components/${id}`),
}

/** One property's own list, which any member may change. */
export const propertyComponentSetApi = {
  get: (propertyId: string) => apiClient.get<PropertyComponentSetDto>(`/properties/${propertyId}/components`),
  create: (propertyId: string, input: PropertyComponentInput) =>
    apiClient.post<PropertyLocalComponentDto>(`/properties/${propertyId}/components`, input),
  update: (propertyId: string, componentId: string, input: PropertyComponentInput) =>
    apiClient.put<void>(`/properties/${propertyId}/components/${componentId}`, input),
  remove: (propertyId: string, componentId: string) =>
    apiClient.delete<void>(`/properties/${propertyId}/components/${componentId}`),
  sync: (propertyId: string) =>
    apiClient.post<PropertyComponentSetDto>(`/properties/${propertyId}/components/sync`, {}),
}
