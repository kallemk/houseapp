import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { budgetsApi, type SaveBudgetInput } from '../api/budgets'

const key = (propertyId: string) => ['budgets', propertyId]

export function useBudgets(propertyId: string) {
  return useQuery({
    queryKey: key(propertyId),
    queryFn: () => budgetsApi.list(propertyId),
    enabled: !!propertyId,
  })
}

export function useSaveBudget(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: SaveBudgetInput) => budgetsApi.save(propertyId, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}

export function useDeleteBudget(propertyId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (year: number) => budgetsApi.remove(propertyId, year),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key(propertyId) }),
  })
}
