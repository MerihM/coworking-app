import { Badge, type MantineColor } from "@mantine/core";
import { useTranslation } from "react-i18next";
import { useApiHealth, type ApiHealth } from "./useApiHealth";

/** Translation key for each state. `satisfies` makes TypeScript complain if a state is missing. */
const labelKeys = {
  checking: "health.checking",
  Healthy: "health.healthy",
  Degraded: "health.degraded",
  Unhealthy: "health.unhealthy",
  unreachable: "health.unreachable",
} as const satisfies Record<ApiHealth, string>;

const colors: Record<ApiHealth, MantineColor> = {
  checking: "gray",
  Healthy: "green",
  Degraded: "yellow",
  Unhealthy: "red",
  unreachable: "red",
};

/** Small badge with the current API status, e.g. "API: Healthy". */
export function ApiStatus() {
  const { t } = useTranslation();
  const status = useApiHealth();

  return (
    // role="status": screen readers announce the change when the text updates.
    <Badge role="status" color={colors[status]} variant="light" size="lg" tt="none">
      {t("health.label")}: {t(labelKeys[status])}
    </Badge>
  );
}
