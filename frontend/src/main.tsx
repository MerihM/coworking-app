// Entry point: mounts the React application into <div id="root"> in index.html.
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { RouterProvider } from "react-router/dom";
import { router } from "./routes/router";
import "./index.css";

createRoot(document.getElementById("root")!).render(
  // StrictMode runs extra checks in development (e.g. effects run twice to expose missing cleanup).
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
);
