import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5085,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5185',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
