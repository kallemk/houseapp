import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { feedbackApi } from '../api/feedback'

const KEY = ['feedback']

export function useFeedback() {
  return useQuery({ queryKey: KEY, queryFn: feedbackApi.list })
}

export function useCreateFeedback() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ title, body }: { title: string; body: string }) => feedbackApi.create(title, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY }),
  })
}
