// Report periods (FR11, CW-33): the presets of the report page as pure functions, so they can be
// tested without rendering. Dates are "YYYY-MM-DD" strings (local Sarajevo days) like everywhere
// else in the UI; the arithmetic runs on UTC midnights, so the browser's own time zone can never
// shift a day.

/** The quick choices above the report. "custom" shows a date-range picker. */
export const reportPresets = ["thisMonth", "lastMonth", "last7", "custom"] as const;

export type ReportPreset = (typeof reportPresets)[number];

/** Both days inclusive, like the API's `from` / `to`. */
export type ReportPeriod = { from: string; to: string };

/** Translation key per preset. */
export const reportPresetLabelKeys = {
  thisMonth: "reports.presets.thisMonth",
  lastMonth: "reports.presets.lastMonth",
  last7: "reports.presets.last7",
  custom: "reports.presets.custom",
} as const satisfies Record<ReportPreset, string>;

export const isReportPreset = (value: string): value is ReportPreset =>
  reportPresets.some((preset) => preset === value);

/** Longest period the API accepts (both ends counted) – the same number as the backend validator. */
export const MAX_REPORT_DAYS = 366;

const toDate = (iso: string) => new Date(`${iso}T00:00:00Z`);
const toIso = (date: Date) => date.toISOString().slice(0, 10);

/** "2026-11-27" + 3 → "2026-11-30" (negative values go back). */
export function shiftDays(iso: string, days: number): string {
  const date = toDate(iso);
  date.setUTCDate(date.getUTCDate() + days);
  return toIso(date);
}

/** The first and last day of the month that contains `iso`, moved by `monthOffset` months. */
function monthOf(iso: string, monthOffset: number): ReportPeriod {
  const date = toDate(iso);
  // Day 1 of the target month; JavaScript rolls month -1 over to December of the previous year.
  const first = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + monthOffset, 1));
  // Day 0 of the next month = the last day of the target month.
  const last = new Date(Date.UTC(first.getUTCFullYear(), first.getUTCMonth() + 1, 0));
  return { from: toIso(first), to: toIso(last) };
}

/**
 * The period of a preset, seen from `today` (the center's local date, see centerNow()).
 * "This month" covers the WHOLE month, so it also shows reservations already booked for its
 * remaining days. "custom" has no period of its own and returns this month as the starting point.
 */
export function presetPeriod(preset: ReportPreset, today: string): ReportPeriod {
  switch (preset) {
    case "lastMonth":
      return monthOf(today, -1);
    case "last7":
      return { from: shiftDays(today, -6), to: today };
    case "thisMonth":
    case "custom":
      return monthOf(today, 0);
  }
}

/** Number of days in a period, both ends counted (1 for a single day). */
export function periodDays({ from, to }: ReportPeriod): number {
  return Math.round((toDate(to).getTime() - toDate(from).getTime()) / 86_400_000) + 1;
}
