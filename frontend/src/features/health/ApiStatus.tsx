import { useApiHealth, type ApiHealth } from "./useApiHealth";

const labels: Record<ApiHealth, string> = {
  checking: "checking…",
  Healthy: "Healthy",
  Degraded: "Degraded",
  Unhealthy: "Unhealthy (database not reachable)",
  unreachable: "unreachable (is the API running?)",
};

/** Small badge with the current API status, e.g. "API: Healthy". */
export function ApiStatus() {
  const status = useApiHealth();

  return (
    // role="status": screen readers announce the change when the text updates.
    <p className={`api-status api-status--${status.toLowerCase()}`} role="status">
      API: {labels[status]}
    </p>
  );
}
