import { describe, expect, it } from "vitest";
import {
  shortTime,
  slotAtTime,
  slotWithWorkspace,
  toAlternativesQuery,
  toCreateRequest,
  type ReservationSlot,
} from "./slot";

const slot: ReservationSlot = {
  workspace: { id: 7, code: "OS-01", type: "HotDesk", capacity: 1, roomName: "Open Space" },
  date: "2026-11-30",
  from: "13:00",
  to: "15:00",
};

describe("reservation slot helpers", () => {
  it("sends times with seconds in the POST body and without them in the alternatives query", () => {
    expect(toCreateRequest(slot)).toEqual({
      workspaceId: 7,
      date: "2026-11-30",
      from: "13:00:00",
      to: "15:00:00",
    });
    expect(toAlternativesQuery(slot)).toEqual({
      WorkspaceId: 7,
      Date: "2026-11-30",
      From: "13:00",
      To: "15:00",
    });
  });

  it("shortens API times", () => {
    expect(shortTime("07:45:00")).toBe("07:45");
  });

  it("turns a 'same workspace, other time' offer into a slot", () => {
    const offer = slotAtTime(slot, {
      date: "2026-12-01",
      from: "07:00:00",
      to: "09:00:00",
      startsAt: "2026-12-01T06:00:00Z",
      endsAt: "2026-12-01T08:00:00Z",
    });

    expect(offer).toEqual({ ...slot, date: "2026-12-01", from: "07:00", to: "09:00" });
  });

  it("turns an 'other workspace, same time' offer into a slot", () => {
    const offer = slotWithWorkspace(slot, {
      id: 8,
      code: "OS-02",
      type: "HotDesk",
      capacity: 1,
      roomId: 1,
      roomName: "Open Space",
    });

    expect(offer.workspace).toEqual({
      id: 8,
      code: "OS-02",
      type: "HotDesk",
      capacity: 1,
      roomName: "Open Space",
    });
    expect([offer.date, offer.from, offer.to]).toEqual(["2026-11-30", "13:00", "15:00"]);
  });
});
