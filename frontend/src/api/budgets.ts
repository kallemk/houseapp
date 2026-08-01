import { apiClient } from './client'
import type { BudgetDto } from './types'

export interface SaveBudgetInput {
  year: number
  maintenanceBudget: number
  renovationBudget: number
  investmentBudget: number
  purchaseBudget: number
}

export const budgetsApi = {
  list: (propertyId: string) => apiClient.get<BudgetDto[]>(`/properties/${propertyId}/budgets`),
  getForYear: (propertyId: string, year: number) =>
    apiClient.get<BudgetDto>(`/properties/${propertyId}/budgets/${year}`),
  /** Upsert — one budget per property per year. */
  save: (propertyId: string, input: SaveBudgetInput) =>
    apiClient.put<BudgetDto>(`/properties/${propertyId}/budgets/${input.year}`, input),
  remove: (propertyId: string, year: number) =>
    apiClient.delete<void>(`/properties/${propertyId}/budgets/${year}`),
}
