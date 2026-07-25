import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

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
    },
  },
})
