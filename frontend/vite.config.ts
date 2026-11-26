/// <reference types="vitest/config" />
// The line above adds the types of the "test" section below (Vitest reads this same file).
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
  // Vitest (CW-15) – https://vitest.dev/config/
  test: {
    // jsdom = a browser-like DOM in Node, so components can render in tests.
    environment: "jsdom",
    // Runs before every test file: jest-dom matchers, browser API stubs, i18n.
    setupFiles: ["./src/test/setup.ts"],
    // Undo vi.spyOn/vi.fn mock implementations after each test, so tests can't affect each other.
    restoreMocks: true,
  },
});
