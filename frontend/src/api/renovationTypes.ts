import { apiClient } from './client'
import type { RenovationTypeDto } from './types'

export interface RenovationTypeInput {
  name: string
  recommendedIntervalMonths: number | null
}

export const renovationTypesApi = {
  list: () => apiClient.get<RenovationTypeDto[]>('/renovation-types'),
  create: (input: RenovationTypeInput) => apiClient.post<RenovationTypeDto>('/renovation-types', input),
  update: (id: string, input: RenovationTypeInput) => apiClient.put<void>(`/renovation-types/${id}`, input),
  remove: (id: string) => apiClient.delete<void>(`/renovation-types/${id}`),
}
