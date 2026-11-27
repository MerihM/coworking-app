import { Badge } from "@mantine/core";
import { useTranslation } from "react-i18next";
import type { MembershipStatus } from "./membershipsApi";
import { membershipStatusColors, membershipStatusLabelKeys } from "./membershipStatus";

/** "Unpaid" (yellow) / "Active" (green) / "Expired" (gray). Used by members and reception. */
export function MembershipStatusBadge({ status }: { status: MembershipStatus }) {
  const { t } = useTranslation();
  return (
    <Badge color={membershipStatusColors[status]} variant="light">
      {t(membershipStatusLabelKeys[status])}
    </Badge>
  );
}
