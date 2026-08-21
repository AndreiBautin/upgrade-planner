import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

/**
 * `base` is the single source of truth for where the app is served from.
 *
 * Vite derives `import.meta.env.BASE_URL` from it, and the router reads its
 * basename from that same value (see src/config.ts), so the bundler's asset URLs
 * and the client-side routes cannot drift apart. On GitHub Pages a project site
 * lives at /<repo>/, so the deploy workflow sets VITE_BASE_PATH=/upgrade-planner/.
 * Locally it stays "/".
 */
const basePath = process.env.VITE_BASE_PATH || '/'

// https://vite.dev/config/
export default defineConfig({
  base: basePath,
  plugins: [react()],
  build: {
    // Static hosts serve stale HTML from caches for a while; a source map makes a
    // production stack trace readable without shipping the original source.
    sourcemap: true,
  },
  server: {
    port: 5176,
    strictPort: true,
    proxy: {
      // In development the API is same-origin through this proxy, which is why
      // VITE_API_BASE_URL is empty locally and CORS never comes into play.
      '/api': {
        target: 'http://localhost:5131',
        changeOrigin: true,
      },
    },
  },
})
