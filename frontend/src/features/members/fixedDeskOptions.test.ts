import { describe, expect, it } from "vitest";
import { fixedDeskOptions } from "./fixedDeskOptions";
import type { FixedDeskOption, Member } from "./membersApi";

const desks: FixedDeskOption[] = [
  {
    id: 25,
    code: "OS-25",
    roomName: "Open Space",
    assignedMemberId: null,
    assignedMemberName: null,
  },
  {
    id: 26,
    code: "OS-26",
    roomName: "Open Space",
    assignedMemberId: 4,
    assignedMemberName: "Emir K",
  },
];

function member(overrides: Partial<Member>): Member {
  return {
    id: 7,
    firstName: "Amra",
    lastName: "Hodžić",
    email: "amra@example.com",
    phone: "+387 61 123 456",
    isActive: true,
    registeredAt: "2026-11-27T09:00:00Z",
    fixedDesk: null,
    currentMembership: null,
    validThrough: null,
    hasUnpaidMembership: false,
    ...overrides,
  };
}

describe("fixedDeskOptions", () => {
  it("disables desks of other members and names the holder", () => {
    expect(fixedDeskOptions(desks, null)).toEqual([
      { value: "25", label: "OS-25 – Open Space", disabled: false },
      { value: "26", label: "OS-26 – Open Space (Emir K)", disabled: true },
    ]);
  });

  it("keeps the member's own desk selectable", () => {
    const holder = member({ id: 4, fixedDesk: { id: 26, code: "OS-26" } });

    expect(fixedDeskOptions(desks, holder)[1]).toEqual({
      value: "26",
      label: "OS-26 – Open Space",
      disabled: false,
    });
  });

  it("adds the member's own desk when the API no longer lists it (e.g. room deactivated)", () => {
    const holder = member({ fixedDesk: { id: 99, code: "OS-99" } });

    expect(fixedDeskOptions(desks, holder)[0]).toEqual({
      value: "99",
      label: "OS-99",
      disabled: false,
    });
  });
});
