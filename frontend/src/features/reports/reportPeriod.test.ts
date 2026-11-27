import { describe, expect, it } from "vitest";
import { isReportPreset, periodDays, presetPeriod, shiftDays } from "./reportPeriod";

describe("presetPeriod", () => {
  const today = "2026-11-27";

  it("this month = the whole calendar month", () => {
    expect(presetPeriod("thisMonth", today)).toEqual({ from: "2026-11-01", to: "2026-11-30" });
  });

  it("last month, also across a year and in February", () => {
    expect(presetPeriod("lastMonth", today)).toEqual({ from: "2026-10-01", to: "2026-10-31" });
    expect(presetPeriod("lastMonth", "2027-01-15")).toEqual({
      from: "2026-12-01",
      to: "2026-12-31",
    });
    expect(presetPeriod("lastMonth", "2028-03-31")).toEqual({
      from: "2028-02-01",
      to: "2028-02-29", // leap year
    });
  });

  it("last 7 days ends today", () => {
    expect(presetPeriod("last7", today)).toEqual({ from: "2026-11-21", to: "2026-11-27" });
  });

  it("custom starts from this month", () => {
    expect(presetPeriod("custom", today)).toEqual(presetPeriod("thisMonth", today));
  });
});

describe("helpers", () => {
  it("shiftDays crosses months and years", () => {
    expect(shiftDays("2026-11-30", 1)).toBe("2026-12-01");
    expect(shiftDays("2027-01-01", -1)).toBe("2026-12-31");
  });

  it("periodDays counts both ends, also over the DST change", () => {
    expect(periodDays({ from: "2026-11-27", to: "2026-11-27" })).toBe(1);
    expect(periodDays({ from: "2026-03-01", to: "2026-03-31" })).toBe(31);
    expect(periodDays({ from: "2028-01-01", to: "2028-12-31" })).toBe(366);
  });

  it("isReportPreset accepts only the presets", () => {
    expect(isReportPreset("last7")).toBe(true);
    expect(isReportPreset("yesterday")).toBe(false);
  });
});
