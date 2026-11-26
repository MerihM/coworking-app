import { Badge } from "@mantine/core";
import { useTranslation } from "react-i18next";

type StatusBadgeProps = {
  active: boolean;
};

/** "Active" (green) / "Inactive" (gray) – the same badge for rooms, workspaces, membership types. */
export function StatusBadge({ active }: StatusBadgeProps) {
  const { t } = useTranslation();
  return (
    <Badge color={active ? "green" : "gray"} variant="light">
      {active ? t("common.active") : t("common.inactive")}
    </Badge>
  );
}
