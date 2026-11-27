import { describe, expect, it } from "vitest";
import type { Membership } from "./membershipsApi";
import { paymentStartDate, summarizeMemberships } from "./membershipStatus";

/** A membership with sensible defaults; only the fields a test cares about are passed. */
function membership(overrides: Partial<Membership>): Membership {
  return {
    id: 1,
    memberId: 7,
    memberName: "Amra Hodžić",
    memberEmail: "amra@example.com",
    membershipTypeId: 3,
    typeName: "Monthly",
    durationDays: 30,
    price: 180,
    status: "Active",
    purchasedAt: "2026-11-27T09:00:00Z",
    startDate: "2026-11-27",
    endDate: "2026-12-27",
    lastDay: "2026-12-26",
    payment: null,
    ...overrides,
  };
}

describe("summarizeMemberships", () => {
  it("finds the membership valid today and its last day", () => {
    const summary = summarizeMemberships([membership({})], "2026-12-01");

    expect(summary.current?.id).toBe(1);
    expect(summary.validThrough).toBe("2026-12-26");
    expect(summary.upcoming).toBeUndefined();
    expect(summary.unpaid).toBeUndefined();
  });

  it("treats the end date as exclusive (the day after the last day is not valid)", () => {
    const summary = summarizeMemberships([membership({})], "2026-12-27");

    expect(summary.current).toBeUndefined();
    expect(summary.validThrough).toBeUndefined();
  });

  it("extends 'valid through' with a paid renewal that starts later (BR05)", () => {
    const renewal = membership({
      id: 2,
      startDate: "2026-12-27",
      endDate: "2027-01-26",
      lastDay: "2027-01-25",
    });

    const summary = summarizeMemberships([renewal, membership({})], "2026-12-01");

    expect(summary.current?.id).toBe(1);
    expect(summary.upcoming?.id).toBe(2);
    expect(summary.validThrough).toBe("2027-01-25");
  });

  it("reports an unpaid order, which never counts as valid", () => {
    const unpaid = membership({
      id: 5,
      status: "Unpaid",
      startDate: null,
      endDate: null,
      lastDay: null,
    });

    const summary = summarizeMemberships([unpaid], "2026-12-01");

    expect(summary.unpaid?.id).toBe(5);
    expect(summary.current).toBeUndefined();
  });
});

describe("paymentStartDate (BR05 preview)", () => {
  it("starts today when there is no valid membership", () => {
    expect(paymentStartDate(null, "2026-12-01")).toBe("2026-12-01");
    expect(paymentStartDate("2026-11-30", "2026-12-01")).toBe("2026-12-01");
  });

  it("starts the day after the current membership's last day (renewal)", () => {
    expect(paymentStartDate("2026-12-26", "2026-12-01")).toBe("2026-12-27");
    expect(paymentStartDate("2026-12-01", "2026-12-01")).toBe("2026-12-02");
  });
});
