// Entry point: loads the translations, then mounts the React application into <div id="root">.

// Mantine's base styles. Imported first, so our own styles (if any) can override them.
import "@mantine/core/styles.css";
import "@mantine/notifications/styles.css";
import "@mantine/dates/styles.css";

import { MantineProvider } from "@mantine/core";
import { Notifications } from "@mantine/notifications";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { RouterProvider } from "react-router/dom";
import { LocalizedDatesProvider } from "./components/LocalizedDatesProvider";
import { AuthProvider } from "./features/auth/AuthProvider";
import { i18nReady } from "./i18n";
import { router } from "./routes/router";
import { theme } from "./theme";

// Top-level await: wait until i18next has picked the language and loaded the texts, so the
// first screen is already translated (no flash of raw keys).
await i18nReady;

createRoot(document.getElementById("root")!).render(
  // StrictMode runs extra checks in development (e.g. effects run twice to expose missing cleanup).
  <StrictMode>
    {/* defaultColorScheme="auto": light or dark, following the operating system. */}
    <MantineProvider theme={theme} defaultColorScheme="auto">
      {/* One place on screen where notifications (success/error toasts) appear – used from CW-16. */}
      <Notifications position="top-right" />
      {/* Session state for the whole app; starts the silent login (CW-15). */}
      {/* Date pickers in the UI language, weeks starting on Monday (CW-25). */}
      <LocalizedDatesProvider>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </LocalizedDatesProvider>
    </MantineProvider>
  </StrictMode>,
);
