import { describe, expect, it } from "vitest";
import { defaultSearch, readSearch, toApiQuery, writeSearch } from "./searchParams";

const params = (query: string) => new URLSearchParams(query);

describe("readSearch", () => {
  it("reads a complete search", () => {
    expect(
      readSearch(params("date=2026-12-01&from=09:00&to=12:00&type=MeetingRoom&min=6")),
    ).toEqual({
      date: "2026-12-01",
      from: "09:00",
      to: "12:00",
      type: "MeetingRoom",
      minCapacity: 6,
    });
  });

  it("uses 'all types' when the type is missing", () => {
    expect(readSearch(params("date=2026-12-01&from=09:00&to=12:00"))?.type).toBe("all");
  });

  it.each([
    ["missing date", "from=09:00&to=12:00"],
    ["malformed date", "date=01.12.2026&from=09:00&to=12:00"],
    ["malformed time", "date=2026-12-01&from=9&to=12:00"],
    ["fixed desk", "date=2026-12-01&from=09:00&to=12:00&type=FixedDesk"],
    ["unknown type", "date=2026-12-01&from=09:00&to=12:00&type=Sauna"],
  ])("returns null for %s", (_, query) => {
    expect(readSearch(params(query))).toBeNull();
  });

  it("ignores a minimum capacity for desks and outside 1–50", () => {
    expect(
      readSearch(params("date=2026-12-01&from=09:00&to=12:00&type=HotDesk&min=4"))?.minCapacity,
    ).toBeNull();
    expect(
      readSearch(params("date=2026-12-01&from=09:00&to=12:00&type=Office&min=99"))?.minCapacity,
    ).toBeNull();
  });
});

describe("writeSearch", () => {
  it("round-trips through readSearch and leaves defaults out", () => {
    const search = {
      date: "2026-12-01",
      from: "09:00",
      to: "12:00",
      type: "all",
      minCapacity: null,
    } as const;
    const written = writeSearch(search);

    expect(written.has("type")).toBe(false);
    expect(written.has("min")).toBe(false);
    expect(readSearch(written)).toEqual(search);
  });
});

describe("toApiQuery", () => {
  it("uses the API's parameter names", () => {
    expect(
      toApiQuery({
        date: "2026-12-01",
        from: "09:00",
        to: "12:00",
        type: "Office",
        minCapacity: 4,
      }),
    ).toEqual({ Date: "2026-12-01", From: "09:00", To: "12:00", Type: "Office", MinCapacity: 4 });
  });

  it("sends no type for 'all types'", () => {
    expect(
      toApiQuery({ date: "2026-12-01", from: "09:00", to: "12:00", type: "all", minCapacity: 3 }),
    ).toEqual({ Date: "2026-12-01", From: "09:00", To: "12:00" });
  });
});

describe("defaultSearch", () => {
  it("suggests 09:00–11:00 on the next open day", () => {
    // Saturday 2026-11-28 at 18:00: Saturday is over, Sunday is closed → Monday.
    expect(defaultSearch({ date: "2026-11-28", minutes: 18 * 60 })).toEqual({
      date: "2026-11-30",
      from: "09:00",
      to: "11:00",
      type: "all",
      minCapacity: null,
    });
  });
});
