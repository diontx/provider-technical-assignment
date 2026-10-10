import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The browser only ever talks to the Vite server. Requests under /api are forwarded to the
// ASP.NET Core API, so the UI uses relative URLs and no CORS configuration is needed.
// The default matches the API's "http" launch profile; override with API_PROXY_TARGET.
const apiProxy = {
  '/api': {
    target: process.env.API_PROXY_TARGET ?? 'http://localhost:5264',
    changeOrigin: true,
  },
}

export default defineConfig({
  plugins: [react()],
  server: { proxy: apiProxy },
  preview: { proxy: apiProxy },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.js',
    restoreMocks: true,
    unstubGlobals: true,
  },
})
