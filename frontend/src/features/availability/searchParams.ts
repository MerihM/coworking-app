// The search lives in the URL (/availability?date=2026-12-01&from=09:00&to=12:00&type=Office&min=4),
// so a result can be bookmarked, shared or reopened with the browser's Back button.
// This file converts between the URL, the form's values and the API query.
import type { AvailabilityApiQuery } from "./availabilityApi";
import { fitTimes, firstSelectableDate, isIsoDate, isTime, type CenterNow } from "./workingHours";

/** Types a visitor can search for. FixedDesk is assigned to members, not booked (API refuses it). */
export const searchTypes = ["all", "HotDesk", "Office", "MeetingRoom"] as const;
export type SearchType = (typeof searchTypes)[number];

/** Smallest and largest "at least N people" the API accepts. */
export const MIN_CAPACITY_RANGE = { min: 1, max: 50 } as const;

/** The form's values – and what the URL stores. */
export type AvailabilitySearch = {
  date: string;
  from: string;
  to: string;
  type: SearchType;
  /** Only for offices and meeting rooms; null = any size. */
  minCapacity: number | null;
};

export const isSearchType = (value: string | null): value is SearchType =>
  value !== null && (searchTypes as readonly string[]).includes(value);

/** Desks are for one person, so "at least N people" only makes sense for these two. */
export const hasCapacityFilter = (type: SearchType) => type === "Office" || type === "MeetingRoom";

/** A whole number in the API's range, or null. */
function parseMinCapacity(value: string | null): number | null {
  if (value === null || !/^\d+$/.test(value)) {
    return null;
  }
  const number = Number(value);
  return number >= MIN_CAPACITY_RANGE.min && number <= MIN_CAPACITY_RANGE.max ? number : null;
}

/**
 * Reads a search from the URL. Returns null when something required is missing or malformed –
 * the page then shows the empty form instead of sending a request that can only fail.
 * Working hours are NOT checked here: the API answers with BR04, and the page shows that message.
 */
export function readSearch(params: URLSearchParams): AvailabilitySearch | null {
  const date = params.get("date") ?? "";
  const from = params.get("from") ?? "";
  const to = params.get("to") ?? "";
  const rawType = params.get("type") ?? "all";

  if (!isIsoDate(date) || !isTime(from) || !isTime(to) || !isSearchType(rawType)) {
    return null;
  }

  return {
    date,
    from,
    to,
    type: rawType,
    minCapacity: hasCapacityFilter(rawType) ? parseMinCapacity(params.get("min")) : null,
  };
}

/** Writes a search to URL parameters; defaults (all types, any size) are left out. */
export function writeSearch(search: AvailabilitySearch): URLSearchParams {
  const params = new URLSearchParams({ date: search.date, from: search.from, to: search.to });
  if (search.type !== "all") {
    params.set("type", search.type);
  }
  if (hasCapacityFilter(search.type) && search.minCapacity !== null) {
    params.set("min", String(search.minCapacity));
  }
  return params;
}

/** The query for GET /api/availability – parameter names exactly as the API expects them. */
export function toApiQuery(search: AvailabilitySearch): AvailabilityApiQuery {
  const query: AvailabilityApiQuery = { Date: search.date, From: search.from, To: search.to };
  if (search.type !== "all") {
    query.Type = search.type;
  }
  if (hasCapacityFilter(search.type) && search.minCapacity !== null) {
    query.MinCapacity = search.minCapacity;
  }
  return query;
}

/** What the form shows before the first search: the next open day, 09:00–11:00 if possible. */
export function defaultSearch(now: CenterNow): AvailabilitySearch {
  const date = firstSelectableDate(now);
  return { date, ...fitTimes(date, "09:00", "11:00", now), type: "all", minCapacity: null };
}
