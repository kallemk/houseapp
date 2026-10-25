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
  /**
   * Downloads everything about the property as a zip. A top-level navigation rather than a fetch,
   * like `driveApi.connect`: the response is an attachment, so the page stays put, and the browser's
   * own download manager handles a file that can run to hundreds of megabytes.
   */
  export(id: string) {
    window.location.href = `/api/properties/${encodeURIComponent(id)}/export`
  },
}
