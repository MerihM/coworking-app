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
