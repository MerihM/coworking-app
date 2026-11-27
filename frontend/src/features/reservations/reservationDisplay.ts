// Pure helpers for showing reservations (no React): status labels and colors, and the
// "Monday 30 November, 13:00–17:00" line used by the member's and the reception's lists.
import type { TFunction } from "i18next";
import { formatLongDate } from "../../lib/format";
import type { Reservation, ReservationStatus } from "./reservationsApi";
import { shortTime } from "./slot";

/** Translation key per status (the API sends the English enum name; Completed is derived). */
export const reservationStatusLabelKeys = {
  Confirmed: "reservations.status.Confirmed",
  Cancelled: "reservations.status.Cancelled",
  Completed: "reservations.status.Completed",
} as const satisfies Record<ReservationStatus, string>;

/** Badge color per status: coming = green, used = blue, cancelled = gray. */
export const reservationStatusColors = {
  Confirmed: "green",
  Cancelled: "gray",
  Completed: "blue",
} as const satisfies Record<ReservationStatus, string>;

/** The statuses in the order the reception's filter offers them. */
export const reservationStatuses = [
  "Confirmed",
  "Completed",
  "Cancelled",
] as const satisfies readonly ReservationStatus[];

/** "Monday 30 November, 13:00–17:00" in the UI language (local Sarajevo date and times). */
export function formatReservationWhen(
  reservation: Pick<Reservation, "date" | "from" | "to">,
  t: TFunction,
  language: string | undefined,
): string {
  return t("availability.period", {
    date: formatLongDate(reservation.date, language),
    from: shortTime(reservation.from),
    to: shortTime(reservation.to),
  });
}
