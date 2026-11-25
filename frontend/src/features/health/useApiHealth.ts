import { useEffect, useState } from "react";

/**
 * Possible API states:
 * - "Healthy" / "Degraded" / "Unhealthy": the text returned by ASP.NET Core's /health endpoint
 *   ("Unhealthy" = API runs but e.g. PostgreSQL is down)
 * - "checking": request still running
 * - "unreachable": no answer from the API at all (not started, network error)
 */
export type ApiHealth = "checking" | "Healthy" | "Degraded" | "Unhealthy" | "unreachable";

const knownStates: readonly string[] = ["Healthy", "Degraded", "Unhealthy"];

/** Custom hook: asks the API's /health endpoint once when the component appears. */
export function useApiHealth(): ApiHealth {
  const [status, setStatus] = useState<ApiHealth>("checking");

  useEffect(() => {
    // AbortController cancels the request if the component disappears before the answer arrives
    // (and in development, when StrictMode runs the effect twice).
    const controller = new AbortController();

    // Plain fetch: /health is not part of the OpenAPI document, so the typed client doesn't know it.
    // The Vite proxy forwards /health to the API on :5080.
    fetch("/health", { signal: controller.signal })
      .then((response) => response.text())
      .then((text) => setStatus(knownStates.includes(text) ? (text as ApiHealth) : "unreachable"))
      .catch(() => {
        if (!controller.signal.aborted) {
          setStatus("unreachable");
        }
      });

    return () => controller.abort();
  }, []);

  return status;
}
