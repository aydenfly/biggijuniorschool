import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In development run the API (dotnet run) and this dev server side by side; /api and /uploads are proxied.
const api = process.env.CMS_API ?? 'http://localhost:5080';

export default defineConfig({
  base: '/admin/',
  plugins: [react()],
  build: { outDir: '../Cms.Api/wwwroot/admin', emptyOutDir: true, sourcemap: false },
  server: { port: 5173, proxy: { '/api': api, '/uploads': api } },
});
