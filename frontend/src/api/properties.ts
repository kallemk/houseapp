import { apiClient } from './client'
import type { PropertyDto, PropertyType } from './types'

export interface CreatePropertyInput {
  nickname: string
  address: string
  address2: string | null
  yearBuilt: number | null
  type: PropertyType | null
  purchaseDate: string
  purchasePrice: number
}

export const propertiesApi = {
  list: () => apiClient.get<PropertyDto[]>('/properties'),
  getById: (id: string) => apiClient.get<PropertyDto>(`/properties/${id}`),
  create: (input: CreatePropertyInput) => apiClient.post<PropertyDto>('/properties', input),
  update: (id: string, input: CreatePropertyInput) => apiClient.put<void>(`/properties/${id}`, input),
  remove: (id: string) => apiClient.delete<void>(`/properties/${id}`),
}
