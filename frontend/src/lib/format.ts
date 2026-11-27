// Display formats shared by several pages (manager price list, public offer on Home, later the
// member's memberships). Formatting follows the UI language, not the browser's locale, so a page
// never mixes "15.00" in one place with "15,00" in another.
import type { TFunction } from "i18next";

/** The center's currency: convertible mark (KM), ISO code BAM. */
export const CURRENCY = "BAM";

/**
 * Which locale to format numbers with for a UI language. "bs" → "bs-BA" gives "15,00 KM"
 * (comma decimals, the local "KM" symbol); everything else → English, "BAM 15.00".
 */
function numberLocale(language: string | undefined): string {
  return language?.startsWith("bs") ? "bs-BA" : "en";
}

/**
 * A price for display, always with 2 decimals: formatPrice(15, "en") → "BAM 15.00",
 * formatPrice(15, "bs") → "15,00 KM". Uses the browser's built-in Intl (no library).
 * @param value Amount in KM, as the API sends it (a JSON number).
 * @param language The current UI language, e.g. i18n.resolvedLanguage.
 */
export function formatPrice(value: number, language: string | undefined): string {
  return new Intl.NumberFormat(numberLocale(language), {
    style: "currency",
    currency: CURRENCY,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}

/**
 * A duration in days with the right plural for the language:
 * en "1 day" / "7 days"; bs "1 dan" / "2 dana" / "5 dana" / "21 dan".
 * i18next picks the form (key suffix _one / _few / _other) with Intl.PluralRules for the
 * language of `t`, so the grammar rules live in the browser, not in our code.
 * @param days Number of days.
 * @param t The translate function (from useTranslation, or i18n.getFixedT("bs") in tests).
 */
export function formatDuration(days: number, t: TFunction): string {
  return t("format.duration", { count: days });
}

/**
 * A calendar date in words, in the UI language: formatLongDate("2026-12-01", "en") →
 * "Tuesday 1 December", formatLongDate("2026-12-01", "bs") → "utorak, 1. decembar".
 * @param isoDate A local date as "YYYY-MM-DD" (the API's and the date picker's format).
 * @param language The current UI language, e.g. i18n.resolvedLanguage.
 */
export function formatLongDate(isoDate: string, language: string | undefined): string {
  // Midnight UTC + timeZone "UTC": the visitor's own time zone can't move the date by a day.
  return new Intl.DateTimeFormat(language?.startsWith("bs") ? "bs-BA" : "en-GB", {
    weekday: "long",
    day: "numeric",
    month: "long",
    timeZone: "UTC",
  }).format(new Date(`${isoDate}T00:00:00Z`));
}

/**
 * A calendar date, short, in the UI language: formatDate("2026-12-26", "en") → "26 Dec 2026",
 * formatDate("2026-12-26", "bs") → "26. dec 2026." (Used for membership periods, CW-27.)
 * @param isoDate A local date as "YYYY-MM-DD".
 * @param language The current UI language, e.g. i18n.resolvedLanguage.
 */
export function formatDate(isoDate: string, language: string | undefined): string {
  return new Intl.DateTimeFormat(language?.startsWith("bs") ? "bs-BA" : "en-GB", {
    dateStyle: "medium",
    timeZone: "UTC", // same trick as formatLongDate: the visitor's time zone can't move the day
  }).format(new Date(`${isoDate}T00:00:00Z`));
}
