// Pure helpers about memberships: status colors and labels, and "what is my situation today"
// for the member's page. No React here, so it is easy to unit-test.
import type { Membership, MembershipStatus } from "./membershipsApi";

/** Translation key per status (the API sends the English enum name). */
export const membershipStatusLabelKeys = {
  Unpaid: "memberships.status.Unpaid",
  Active: "memberships.status.Active",
  Expired: "memberships.status.Expired",
} as const satisfies Record<MembershipStatus, string>;

/** Badge color per status: waiting = yellow, valid = green, history = gray. */
export const membershipStatusColors = {
  Unpaid: "yellow",
  Active: "green",
  Expired: "gray",
} as const satisfies Record<MembershipStatus, string>;

/** A paid membership: the API always sends its period (null only while Unpaid). */
type PaidMembership = Membership & { startDate: string; endDate: string };

export type MembershipSummary = {
  /** The membership valid today (BR02 – what allows reservations), if any. */
  current: Membership | undefined;
  /**
   * The last valid day counting paid renewals that start later (BR05: a renewal starts when the
   * current membership ends). Equals current.lastDay when there is no renewal.
   */
  validThrough: string | undefined;
  /** A paid renewal that hasn't started yet (shown as "starts on …"). */
  upcoming: Membership | undefined;
  /** The order waiting for payment at the reception (the API allows only one). */
  unpaid: Membership | undefined;
};

/**
 * The member's situation on `today`. Dates are "YYYY-MM-DD" strings, which compare correctly as
 * plain strings. EndDate is exclusive (ADR 0002): valid while startDate ≤ today < endDate.
 * @param memberships The member's memberships, as the API returns them.
 * @param today The Sarajevo date (centerNow().date).
 */
export function summarizeMemberships(memberships: Membership[], today: string): MembershipSummary {
  const paid = memberships.filter(
    (m): m is PaidMembership =>
      m.status === "Active" && m.startDate != null && m.endDate != null && m.endDate > today,
  );

  const current = paid.find((m) => m.startDate <= today);
  const upcoming = paid
    .filter((m) => m.startDate > today)
    .sort((a, b) => a.startDate.localeCompare(b.startDate))[0];

  // The latest exclusive end, minus one day = the last valid day.
  const latestEnd = paid
    .map((m) => m.endDate)
    .sort((a, b) => a.localeCompare(b))
    .at(-1);

  return {
    current,
    validThrough: latestEnd === undefined ? undefined : previousDay(latestEnd),
    upcoming,
    unpaid: memberships.find((m) => m.status === "Unpaid"),
  };
}

/** "2026-12-27" → "2026-12-26", in UTC so summer time can't shift the day. */
function previousDay(isoDate: string): string {
  const date = new Date(`${isoDate}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() - 1);
  return date.toISOString().slice(0, 10);
}

/**
 * When will a membership paid TODAY start? BR05: on the payment day, or – when the member still
 * has a valid paid membership – on the day after its last day (renewal). Used for the preview in
 * the payment dialog (CW-28); the backend calculates the real date.
 * @param validThrough The member's last valid day (MemberResponse.validThrough), or null.
 * @param today The Sarajevo date (centerNow().date).
 */
export function paymentStartDate(validThrough: string | null | undefined, today: string): string {
  if (validThrough == null || validThrough < today) {
    return today;
  }
  const next = new Date(`${validThrough}T00:00:00Z`);
  next.setUTCDate(next.getUTCDate() + 1);
  return next.toISOString().slice(0, 10);
}
