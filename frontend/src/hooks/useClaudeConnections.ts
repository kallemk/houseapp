import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { claudeApi } from '../api/claude'

const key = ['claude-connections']

/** The user's connected Claude clients — shown on /claude, and what decides whether Översikt still suggests connecting. */
export function useClaudeConnections() {
  return useQuery({ queryKey: key, queryFn: claudeApi.connections })
}

export function useDisconnectClaude() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: claudeApi.disconnect,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
  })
}
