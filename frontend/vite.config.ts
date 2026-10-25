import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The backend's MCP and OAuth endpoints describe themselves by the origin they're reached on
// (see McpOAuth.Origin), so these keep the browser's Host and tell the backend the scheme was http.
// changeOrigin would make them announce https://localhost:7275 — where there's no SPA to show the
// consent page on.
const mcpProxy = {
  target: 'https://localhost:7275',
  changeOrigin: false,
  secure: false,
  xfwd: true,
}

// Proxies /api to the local backend so requests are same-origin in dev, just like the Static Web
// App's linked-backend proxy is in production — this is what lets cookie auth use SameSite=Lax
// with no CORS configuration anywhere.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'https://localhost:7275',
        changeOrigin: true,
        secure: false, // accept the local ASP.NET Core dev-cert
      },
      // /oauth/consent is deliberately absent: that one is an SPA page, not a backend route.
      '/mcp': mcpProxy,
      '/oauth/authorize': mcpProxy,
      '/oauth/register': mcpProxy,
      '/oauth/token': mcpProxy,
      '/.well-known': mcpProxy,
    },
  },
})
