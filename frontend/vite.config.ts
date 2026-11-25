import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Vite configuration – https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Fixed port, so the address is always http://localhost:5173
    port: 5173,
    strictPort: true,
    // Dev proxy: the browser only talks to :5173 (same origin, no CORS problems).
    // Vite forwards these paths to the ASP.NET Core API on :5080.
    // In production Nginx does the same job, so frontend code never contains the API's address.
    proxy: {
      "/api": "http://localhost:5080",
      "/health": "http://localhost:5080",
    },
  },
});
