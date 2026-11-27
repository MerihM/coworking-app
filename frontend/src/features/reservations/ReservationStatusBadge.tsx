import { Badge } from "@mantine/core";
import { useTranslation } from "react-i18next";
import { reservationStatusColors, reservationStatusLabelKeys } from "./reservationDisplay";
import type { ReservationStatus } from "./reservationsApi";

/** "Confirmed" (green) / "Completed" (blue) / "Cancelled" (gray). Used by members and reception. */
export function ReservationStatusBadge({ status }: { status: ReservationStatus }) {
  const { t } = useTranslation();
  return (
    <Badge color={reservationStatusColors[status]} variant="light">
      {t(reservationStatusLabelKeys[status])}
    </Badge>
  );
}
