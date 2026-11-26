import { describe, expect, it } from "vitest";
import {
  addDays,
  centerNow,
  firstSelectableDate,
  fitTimes,
  fromOptions,
  isIsoDate,
  isSelectableDate,
  openingHours,
  toOptions,
  type CenterNow,
} from "./workingHours";

// 2026-11-23 is a Monday, 2026-11-28 a Saturday, 2026-11-29 a Sunday.
const MONDAY = "2026-11-23";
const SATURDAY = "2026-11-28";
const SUNDAY = "2026-11-29";

/** "Now" = a Monday morning before the dates above, unless a test needs something else. */
const mondayMorning: CenterNow = { date: MONDAY, minutes: 6 * 60 };

describe("openingHours (BR04)", () => {
  it("knows weekdays, Saturday and Sunday", () => {
    expect(openingHours(MONDAY)).toEqual({ open: 7 * 60, close: 22 * 60 });
    expect(openingHours(SATURDAY)).toEqual({ open: 9 * 60, close: 17 * 60 });
    expect(openingHours(SUNDAY)).toBeNull();
  });
});

describe("fromOptions", () => {
  it("offers weekday starts from 07:00 until 1 hour before closing", () => {
    const options = fromOptions(MONDAY, mondayMorning);
    expect(options[0]).toBe("07:00");
    expect(options.at(-1)).toBe("21:00");
    expect(options).toContain("07:30");
  });

  it("offers Saturday starts from 09:00 to 16:00", () => {
    const options = fromOptions(SATURDAY, mondayMorning);
    expect(options[0]).toBe("09:00");
    expect(options.at(-1)).toBe("16:00");
  });

  it("offers nothing on Sunday or in the past", () => {
    expect(fromOptions(SUNDAY, mondayMorning)).toEqual([]);
    expect(fromOptions(addDays(MONDAY, -1), mondayMorning)).toEqual([]);
  });

  it("starts today with the current half hour (a started period is still searchable)", () => {
    const now: CenterNow = { date: MONDAY, minutes: 10 * 60 + 10 }; // 10:10
    expect(fromOptions(MONDAY, now)[0]).toBe("10:00");
  });

  it("has nothing left today when less than 1 hour remains", () => {
    const now: CenterNow = { date: MONDAY, minutes: 21 * 60 + 31 }; // 21:31
    expect(fromOptions(MONDAY, now)).toEqual([]);
  });
});

describe("toOptions", () => {
  it("ends at least 1 hour after the start and at the latest at closing", () => {
    expect(toOptions(SATURDAY, "15:30")).toEqual(["16:30", "17:00"]);
    expect(toOptions(MONDAY, "09:00")[0]).toBe("10:00");
    expect(toOptions(MONDAY, "09:00").at(-1)).toBe("22:00");
  });

  it("is empty for Sunday or a malformed start", () => {
    expect(toOptions(SUNDAY, "09:00")).toEqual([]);
    expect(toOptions(MONDAY, "9")).toEqual([]);
  });
});

describe("isSelectableDate / firstSelectableDate", () => {
  it("allows today up to 90 days ahead, except Sundays", () => {
    expect(isSelectableDate(MONDAY, mondayMorning)).toBe(true);
    expect(isSelectableDate(SUNDAY, mondayMorning)).toBe(false);
    expect(isSelectableDate(addDays(MONDAY, 89), mondayMorning)).toBe(true); // Saturday 2027-02-20
    expect(isSelectableDate(addDays(MONDAY, 91), mondayMorning)).toBe(false); // Monday, but 91 days
  });

  it("moves to the next open day when today is over", () => {
    const saturdayEvening: CenterNow = { date: SATURDAY, minutes: 16 * 60 + 45 };
    // Saturday is over, Sunday is closed → Monday.
    expect(firstSelectableDate(saturdayEvening)).toBe("2026-11-30");
  });
});

describe("fitTimes", () => {
  it("keeps times that are possible", () => {
    expect(fitTimes(MONDAY, "08:00", "12:00", mondayMorning)).toEqual({
      from: "08:00",
      to: "12:00",
    });
  });

  it("moves impossible times into the opening hours", () => {
    // 19:00 is fine on Monday, but the center closes at 17:00 on Saturday.
    expect(fitTimes(SATURDAY, "19:00", "21:00", mondayMorning)).toEqual({
      from: "09:00",
      to: "11:00",
    });
  });

  it("shortens the period when 2 hours don't fit before closing", () => {
    expect(fitTimes(SATURDAY, "16:00", "18:00", mondayMorning)).toEqual({
      from: "16:00",
      to: "17:00",
    });
  });
});

describe("centerNow", () => {
  it("converts to Sarajevo summer time (+02:00)", () => {
    expect(centerNow(new Date("2026-07-01T10:20:00Z"))).toEqual({
      date: "2026-07-01",
      minutes: 12 * 60 + 20,
    });
  });

  it("converts to Sarajevo winter time (+01:00), across midnight", () => {
    expect(centerNow(new Date("2026-01-15T23:30:00Z"))).toEqual({
      date: "2026-01-16",
      minutes: 30,
    });
  });
});

describe("isIsoDate", () => {
  it("accepts real dates only", () => {
    expect(isIsoDate("2026-02-28")).toBe(true);
    expect(isIsoDate("2026-02-30")).toBe(false);
    expect(isIsoDate("28.02.2026")).toBe(false);
  });
});
