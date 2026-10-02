import { defineConfig } from 'vite';

// base: './' — относительные пути, чтобы сборка работала внутри Android WebView
// через WebViewAssetLoader (https://appassets.androidplatform.net/assets/www/).
export default defineConfig({
  base: './',
  build: {
    target: 'es2020',
    outDir: 'dist',
    assetsInlineLimit: 0,
    chunkSizeWarningLimit: 1200,
    sourcemap: false,
    rollupOptions: {
      output: {
        // three.js — отдельный чанк: кешируется WebView независимо от кода игры
        manualChunks: { three: ['three'] },
      },
    },
  },
  server: { host: true },
});
