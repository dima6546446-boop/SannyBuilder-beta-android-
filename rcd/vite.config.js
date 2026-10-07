import { defineConfig } from 'vite';
export default defineConfig({
  server: { host: true, port: 5173, strictPort: false },
  preview: { host: true, port: 4173 },
  build: { target: 'es2022', chunkSizeWarningLimit: 1500 },
});
