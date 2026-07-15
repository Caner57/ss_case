import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The dev server runs on 5173, the origin the Admin API's CORS policy allows in development.
// VITE_API_BASE_URL points the browser at the Admin API (defaults to the local API port).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
  },
});
