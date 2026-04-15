import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) return
          if (id.includes('lucide-react')) return 'icons'
          return 'vendor'
        },
      },
    },
  },
  server: {
    proxy: {
      // 127.0.0.1 avoids occasional IPv6 localhost resolution issues on Windows.
      '/api': { target: 'http://127.0.0.1:5221', changeOrigin: true, secure: false },
      '/verify': { target: 'http://127.0.0.1:5221', changeOrigin: true, secure: false },
    },
  },
})
