import { describe, expect, it } from "vitest";
import i18n from "../i18n";
import { formatDate, formatDuration, formatLongDate, formatPrice } from "./format";

/** Intl puts a no-break space between number and currency; compare with plain spaces. */
const plain = (text: string) => text.replace(/\s/g, " ");

describe("formatPrice", () => {
  it("formats in English with the ISO code", () => {
    expect(plain(formatPrice(15, "en"))).toBe("BAM 15.00");
    expect(plain(formatPrice(175.5, "en"))).toBe("BAM 175.50");
  });

  it("formats in Bosnian with a decimal comma and KM", () => {
    expect(plain(formatPrice(15, "bs"))).toBe("15,00 KM");
    expect(plain(formatPrice(175.5, "bs"))).toBe("175,50 KM");
  });

  it("treats regional variants like the language", () => {
    expect(plain(formatPrice(15, "bs-BA"))).toBe("15,00 KM");
  });
});

describe("formatDuration", () => {
  it("uses English plurals", () => {
    const t = i18n.getFixedT("en");
    expect(formatDuration(1, t)).toBe("1 day");
    expect(formatDuration(7, t)).toBe("7 days");
  });

  it.each([
    [1, "1 dan"],
    [2, "2 dana"],
    [4, "4 dana"],
    [5, "5 dana"],
    [11, "11 dana"], // 11 is NOT "one" in Bosnian, even though it ends in 1
    [21, "21 dan"], // 21 IS "one"
    [30, "30 dana"],
  ])("uses Bosnian plurals: %i → %s", (days, expected) => {
    expect(formatDuration(days, i18n.getFixedT("bs"))).toBe(expected);
  });
});

describe("formatLongDate", () => {
  it("writes the date in English", () => {
    expect(formatLongDate("2026-12-01", "en")).toBe("Tuesday 1 December");
  });

  it("writes the date in Bosnian", () => {
    expect(formatLongDate("2026-12-01", "bs")).toBe("utorak, 1. decembar");
  });
});

describe("formatDate", () => {
  it("writes a short English date", () => {
    expect(formatDate("2026-12-26", "en")).toBe("26 Dec 2026");
  });

  it("keeps the same day in Bosnian (exact punctuation depends on the browser's Intl data)", () => {
    const text = formatDate("2026-12-26", "bs");
    expect(text).toContain("26");
    expect(text).toContain("2026");
  });
});
