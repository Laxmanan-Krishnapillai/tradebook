import { defineConfig } from 'vitest/config';
import { tanstackRouter } from '@tanstack/router-plugin/vite';

export default defineConfig({
  plugins: [
    tanstackRouter({
      target: 'react',
      autoCodeSplitting: true,
      routesDirectory: './src/app/routes',
      generatedRouteTree: './src/app/routeTree.gen.ts',
      quoteStyle: 'single',
    }),
  ],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./tests/setup.ts'],
    maxWorkers: 1,
  },
  server: {
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://127.0.0.1:5000',
        changeOrigin: true,
      },
      '/hubs': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://127.0.0.1:5000',
        changeOrigin: true,
        ws: true,
      },
    },
  },
});
