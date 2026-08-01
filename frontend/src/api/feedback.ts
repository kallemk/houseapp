import { apiClient } from './client'
import type { FeedbackItemDto } from './types'

export const feedbackApi = {
  list: () => apiClient.get<FeedbackItemDto[]>('/feedback'),
  create: (title: string, body: string) => apiClient.post<FeedbackItemDto>('/feedback', { title, body }),
}
