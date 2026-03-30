import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/accounts': {
        target: 'http://localhost:5289',
        changeOrigin: true,
        rewrite: (path) => '/api' + path,
        secure: false,
      },
      '/account': {
        target: 'http://localhost:5289',
        changeOrigin: true,
        rewrite: (path) => '/api' + path,
        secure: false,
      },
      '/instruments': {
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
      '/orders': {
        target: 'http://localhost:5289',
        changeOrigin: true,
        rewrite: (path) => '/api' + path,
        secure: false,
        ws: true,
      },
      '/order': {
        target: 'http://localhost:5289',
        changeOrigin: true,
        rewrite: (path) => '/api' + path,
        secure: false,
        ws: true,
      },
      '/quotes': {
        target: 'http://localhost:5289',
        changeOrigin: true,
        rewrite: (path) => '/api' + path,
        secure: false,
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
