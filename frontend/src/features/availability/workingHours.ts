// The center's working hours (BR04) for the search form. The rules MIRROR the backend
// (Coworking.Domain.Common.WorkingHours): weekdays 07:00–22:00, Saturday 09:00–17:00, closed on
// Sunday, at least 1 hour, in Sarajevo local time. The form only offers times that pass BR04;
// the API still checks everything (a hand-edited URL gets the BR04 message from the API).
//
// Dates are "YYYY-MM-DD" strings and times "HH:mm" strings – the same format as the API query and
// Mantine's date picker, so no time zone conversion happens in the browser. Plain strings in this
// format also compare correctly with < and >.

/** The center's time zone (ADR 0002). */
export const CENTER_TIME_ZONE = "Europe/Sarajevo";

/** The form offers start/end times in these steps (the API accepts 15, 30 is easier to pick). */
export const SLOT_MINUTES = 30;

/** Shortest reservation (BR04). */
export const MIN_DURATION_MINUTES = 60;

/** The API refuses searches further ahead (AvailabilityQueryValidator.MaxDaysAhead). */
export const MAX_DAYS_AHEAD = 90;

/** Preferred start and length for a new search: 09:00, 2 hours. */
const DEFAULT_FROM = "09:00";
const DEFAULT_LENGTH_MINUTES = 120;

/** "Now" in the center: local date and minutes since midnight. */
export type CenterNow = { date: string; minutes: number };

const timePattern = /^([01]\d|2[0-3]):[0-5]\d$/;
const datePattern = /^\d{4}-\d{2}-\d{2}$/;

/** "HH:mm" in 24-hour format? */
export const isTime = (value: string) => timePattern.test(value);

/** "YYYY-MM-DD" and a real calendar date? (2026-02-30 is not.) */
export function isIsoDate(value: string): boolean {
  if (!datePattern.test(value)) {
    return false;
  }
  const date = new Date(`${value}T00:00:00Z`);
  return !Number.isNaN(date.getTime()) && date.toISOString().startsWith(value);
}

/** "09:30" → 570. */
export function toMinutes(time: string): number {
  const [hours = "0", minutes = "0"] = time.split(":");
  return Number(hours) * 60 + Number(minutes);
}

/** 570 → "09:30". */
export function toTime(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  return `${String(hours).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}`;
}

/** "2026-11-28" + 3 → "2026-12-01". Calculated in UTC, so summer time can't shift the day. */
export function addDays(date: string, days: number): string {
  const value = new Date(`${date}T00:00:00Z`);
  value.setUTCDate(value.getUTCDate() + days);
  return value.toISOString().slice(0, 10);
}

/** 0 = Sunday … 6 = Saturday, like JavaScript's getDay() and .NET's DayOfWeek. */
function dayOfWeek(date: string): number {
  return new Date(`${date}T00:00:00Z`).getUTCDay();
}

/** Opening and closing time in minutes, or null on Sunday (WorkingHours.GetHours). */
export function openingHours(date: string): { open: number; close: number } | null {
  switch (dayOfWeek(date)) {
    case 0:
      return null;
    case 6:
      return { open: 9 * 60, close: 17 * 60 };
    default:
      return { open: 7 * 60, close: 22 * 60 };
  }
}

/**
 * The current local date and time in Sarajevo, whatever time zone the visitor's computer uses.
 * @param instant The moment to convert; tests pass a fixed one.
 */
export function centerNow(instant: Date = new Date()): CenterNow {
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone: CENTER_TIME_ZONE,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(instant);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((p) => p.type === type)?.value ?? "00";

  return {
    date: `${part("year")}-${part("month")}-${part("day")}`,
    minutes: Number(part("hour")) * 60 + Number(part("minute")),
  };
}

/** Times from `first` to `last` (inclusive) in SLOT_MINUTES steps. */
function slots(first: number, last: number): string[] {
  const result: string[] = [];
  for (let minutes = first; minutes <= last; minutes += SLOT_MINUTES) {
    result.push(toTime(minutes));
  }
  return result;
}

/**
 * Start times for a date: from opening until 1 hour before closing.
 * Today starts with the current half hour – a period that has already started is still
 * searchable (walk-ins), like on the backend; only past dates and Sundays get nothing.
 */
export function fromOptions(date: string, now: CenterNow): string[] {
  const hours = openingHours(date);
  if (hours === null || date < now.date) {
    return [];
  }
  const currentSlot = Math.floor(now.minutes / SLOT_MINUTES) * SLOT_MINUTES;
  const first = date === now.date ? Math.max(hours.open, currentSlot) : hours.open;
  return slots(first, hours.close - MIN_DURATION_MINUTES);
}

/** End times for a start: at least 1 hour later, at the latest at closing time. */
export function toOptions(date: string, from: string): string[] {
  const hours = openingHours(date);
  if (hours === null || !isTime(from)) {
    return [];
  }
  return slots(toMinutes(from) + MIN_DURATION_MINUTES, hours.close);
}

/** Can this date be picked? Not in the past, at most 90 days ahead, open, a start time left. */
export function isSelectableDate(date: string, now: CenterNow): boolean {
  return (
    date >= now.date &&
    date <= addDays(now.date, MAX_DAYS_AHEAD) &&
    fromOptions(date, now).length > 0
  );
}

/** The first date that can be picked: today, or the next open day when today is over. */
export function firstSelectableDate(now: CenterNow): string {
  let date = now.date;
  while (!isSelectableDate(date, now) && date < addDays(now.date, 7)) {
    date = addDays(date, 1);
  }
  return date;
}

/**
 * Keeps `from` and `to` when they are possible on `date`, otherwise picks the nearest sensible
 * ones (09:00 or the first free start, 2 hours or as long as possible). Used when the visitor
 * changes the date or the start, so the two selects never show an impossible combination.
 * A closed day leaves the times as they are (the form disables "Search").
 */
export function fitTimes(
  date: string,
  from: string,
  to: string,
  now: CenterNow,
): { from: string; to: string } {
  const froms = fromOptions(date, now);
  if (froms.length === 0) {
    return { from, to };
  }

  const fittedFrom = froms.includes(from)
    ? from
    : froms.includes(DEFAULT_FROM)
      ? DEFAULT_FROM
      : (froms[0] ?? from);

  const tos = toOptions(date, fittedFrom);
  const preferredTo = toTime(toMinutes(fittedFrom) + DEFAULT_LENGTH_MINUTES);
  const fittedTo = tos.includes(to)
    ? to
    : tos.includes(preferredTo)
      ? preferredTo
      : (tos[tos.length - 1] ?? to);

  return { from: fittedFrom, to: fittedTo };
}
