import { apiClient } from './client'
import type { PropertyDto, PropertyType } from './types'

export interface CreatePropertyInput {
  nickname: string
  address: string
  address2: string | null
  postalCode: string | null
  city: string | null
  country: string | null
  propertyDesignation: string | null
  yearBuilt: number | null
  type: PropertyType | null
  latitude: number | null
  longitude: number | null
  purchaseDate: string
  purchasePrice: number
}

export const propertiesApi = {
  list: () => apiClient.get<PropertyDto[]>('/properties'),
  getById: (id: string) => apiClient.get<PropertyDto>(`/properties/${id}`),
  create: (input: CreatePropertyInput) => apiClient.post<PropertyDto>('/properties', input),
  update: (id: string, input: CreatePropertyInput) => apiClient.put<void>(`/properties/${id}`, input),
  /** `deleteFromDrive` also clears the property's documents out of the connected Google Drive. */
  remove: (id: string, deleteFromDrive = false) =>
    apiClient.delete<void>(`/properties/${id}?deleteFromDrive=${deleteFromDrive}`),
}
