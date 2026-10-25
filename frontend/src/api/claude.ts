import { apiClient } from './client'

export interface ConsentSummary {
  clientName: string
  redirectHost: string
  /** Sent back to this computer rather than claude.ai — Claude Code, or a local tool. */
  isLoopback: boolean
}

export interface ClaudeConnection {
  id: string
  clientName: string
  redirectHost: string
  createdAt: string
  lastUsedAt: string
}

/** The address people paste into Claude: the app's own origin plus /mcp, so it's right in every environment. */
export const mcpServerUrl = () => `${window.location.origin}/mcp`

export const claudeApi = {
  /** What the consent page shows for a request the server has already vetted at /oauth/authorize. */
  consent: (request: string) => apiClient.get<ConsentSummary>(`/oauth/consent?request=${encodeURIComponent(request)}`),
  decide: (request: string, approve: boolean) =>
    apiClient.post<{ redirectUrl: string }>('/oauth/consent', { request, approve }),
  connections: () => apiClient.get<ClaudeConnection[]>('/mcp/connections'),
  disconnect: (id: string) => apiClient.delete<void>(`/mcp/connections/${encodeURIComponent(id)}`),
}
