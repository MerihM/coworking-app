import { describe, expect, it } from "vitest";
import i18n from "../../i18n";
import { workspaceSchema } from "./validation";
import { capacityRange, clampCapacity, normalizeCode } from "./workspaceTypes";

// Same rules as the backend (Workspace.CapacityRange, WorkspaceInputValidator).
const t = i18n.t.bind(i18n);
const schema = workspaceSchema(t);
const valid = { roomId: 3, code: "OS-01", type: "HotDesk" as const, capacity: 1 };

function errorsOf(values: object): Record<string, string> {
  const result = schema.safeParse({ ...valid, ...values });
  return result.success
    ? {}
    : Object.fromEntries(result.error.issues.map((issue) => [issue.path.join("."), issue.message]));
}

describe("workspaceSchema", () => {
  it.each([
    ["HotDesk", 1],
    ["FixedDesk", 1],
    ["Office", 1],
    ["Office", 20],
    ["MeetingRoom", 2],
    ["MeetingRoom", 50],
  ] as const)("accepts %s with capacity %i", (type, capacity) => {
    expect(errorsOf({ type, capacity })).toEqual({});
  });

  it("explains the exact capacity for desks", () => {
    expect(errorsOf({ type: "HotDesk", capacity: 4 })).toEqual({
      capacity: "Must be exactly 1.",
    });
  });

  it.each([
    ["Office", 21, "Between 1 and 20."],
    ["MeetingRoom", 1, "Between 2 and 50."],
  ] as const)("rejects %s with capacity %i", (type, capacity, message) => {
    expect(errorsOf({ type, capacity })).toEqual({ capacity: message });
  });

  it.each(["os-01", " MR-A ", "A1"])("accepts the code %j (checked in upper case)", (code) => {
    expect(errorsOf({ code })).toEqual({});
  });

  it.each(["-A1", "A 1", "Š1", "ABCDEFGHIJKLMNOPQRSTU"])("rejects the code %j", (code) => {
    expect(errorsOf({ code })).toHaveProperty("code");
  });
});

describe("workspace type helpers", () => {
  it("has the same capacity ranges as the backend", () => {
    expect(capacityRange("FixedDesk")).toEqual({ min: 1, max: 1 });
    expect(capacityRange("Office")).toEqual({ min: 1, max: 20 });
    expect(capacityRange("MeetingRoom")).toEqual({ min: 2, max: 50 });
  });

  it("clamps the capacity when the type changes", () => {
    expect(clampCapacity("HotDesk", 6)).toBe(1);
    expect(clampCapacity("MeetingRoom", 1)).toBe(2);
    expect(clampCapacity("Office", 6)).toBe(6);
  });

  it("normalizes codes like the backend", () => {
    expect(normalizeCode("  os-01 ")).toBe("OS-01");
  });
});
