import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: '../../docs/api/typespec/tsp-output/@typespec/openapi3/openapi.yaml',
  output: './src/api/generated',
  plugins: [
    '@hey-api/typescript',
    '@hey-api/client-fetch',
    { name: 'zod' },
    { name: '@hey-api/sdk', validator: true },
    { name: '@tanstack/react-query' },
  ],
});
