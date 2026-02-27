import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // Forward all /api/* calls to the .NET server.
      // This means AgentChat can use apiBase="" (same origin) and no CORS is needed.
      '/api': {
        target: 'http://localhost:5104',
        changeOrigin: true,
        // ws: true proxies WebSocket upgrade requests to the .NET server.
        // Without this, ws://localhost:5173/api/agent/ws/* would hit Vite
        // itself and be rejected immediately.
        ws: true,
        // SSE streams need buffering disabled so events arrive immediately.
        configure: (proxy) => {
          proxy.on('proxyReq', (_proxyReq, req) => {
            if (req.headers.accept?.includes('text/event-stream')) {
              _proxyReq.setHeader('Cache-Control', 'no-cache');
            }
          });
        },
      },
    },
  },
})
