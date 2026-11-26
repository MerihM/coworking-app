// Everything the UI needs to know about workspace types, in one place. The rules MIRROR the
// backend (Workspace.CapacityRange, WorkspaceCodeFormat) so forms can help before the API answers.
import type { WorkspaceType } from "./workspacesApi";

/** All types, in the order shown in selects and filters. */
export const workspaceTypes = [
  "HotDesk",
  "FixedDesk",
  "Office",
  "MeetingRoom",
] as const satisfies readonly WorkspaceType[];

/** Translation key per type ("Hot desk" / "Fleksibilno mjesto"). */
export const workspaceTypeLabelKeys = {
  HotDesk: "workspaces.types.HotDesk",
  FixedDesk: "workspaces.types.FixedDesk",
  Office: "workspaces.types.Office",
  MeetingRoom: "workspaces.types.MeetingRoom",
} as const satisfies Record<WorkspaceType, string>;

/** Badge color per type, so the types are easy to tell apart in a long table. */
export const workspaceTypeColors: Record<WorkspaceType, string> = {
  HotDesk: "blue",
  FixedDesk: "grape",
  Office: "teal",
  MeetingRoom: "orange",
};

/** Desks are for exactly one person. */
export const isDesk = (type: WorkspaceType) => type === "HotDesk" || type === "FixedDesk";

/** Allowed capacity per type – the same numbers as Workspace.CapacityRange on the backend. */
export function capacityRange(type: WorkspaceType): { min: number; max: number } {
  switch (type) {
    case "HotDesk":
    case "FixedDesk":
      return { min: 1, max: 1 };
    case "Office":
      return { min: 1, max: 20 };
    case "MeetingRoom":
      return { min: 2, max: 50 };
  }
}

/** Keeps a capacity inside the range of a type (used when the type changes in the form). */
export function clampCapacity(type: WorkspaceType, capacity: number): number {
  const { min, max } = capacityRange(type);
  return Math.min(Math.max(capacity, min), max);
}

/** Letters A–Z, digits and "-", not starting with "-", 1–20 characters (same as the backend). */
export const workspaceCodePattern = /^[A-Z0-9][A-Z0-9-]{0,19}$/;

/** "  os-01 " → "OS-01" – the form the backend stores. */
export const normalizeCode = (code: string) => code.trim().toUpperCase();
