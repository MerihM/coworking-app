// Runs before every test file (vite.config.ts → test.setupFiles).
import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";
import i18n, { i18nReady } from "../i18n";

// toBeInTheDocument(), toHaveTextContent()... come from the jest-dom import above.

// Browser APIs that Mantine uses but jsdom doesn't implement.
Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
    dispatchEvent: () => false,
  }),
});

class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
window.ResizeObserver = ResizeObserverStub as unknown as typeof ResizeObserver;

// Tests check English texts, whatever language the developer's machine has.
await i18nReady;
await i18n.changeLanguage("en");

// Remove rendered components after each test (automatic only with Vitest "globals", which we don't use).
afterEach(() => cleanup());
