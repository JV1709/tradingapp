import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/account': {
        target: 'http://localhost:5289',
        changeOrigin: true,
        rewrite: (path) => '/api' + path,
        secure: false,
      },
      '/instrument': {
        target: 'http://localhost:5289',
        changeOrigin: true,
        rewrite: (path) => '/api' + path,
        secure: false,
      },
      '/order': {
        target: 'http://localhost:5289',
        changeOrigin: true,
        rewrite: (path) => '/api' + path,
        secure: false,
        ws: true,
      },
      '/quote': {
        target: 'http://localhost:5289',
        changeOrigin: true,
        rewrite: (path) => '/api' + path,
        secure: false,
      },
    },
  },
})
