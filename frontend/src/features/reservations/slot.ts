// A "slot" = one workspace for one local date and time – what the member is about to reserve.
// Pure helpers (no React), so they can be tested directly and the .tsx files stay component-only.
import type { components } from "../../api/schema";
import type {
  AlternativeSlot,
  AlternativeWorkspace,
  AlternativesQuery,
  CreateReservationRequest,
} from "./reservationsApi";

/** The workspace details the confirmation shows. */
export type SlotWorkspace = {
  id: number;
  code: string;
  type: components["schemas"]["WorkspaceType"];
  capacity: number;
  roomName: string;
};

/** Local Sarajevo date "YYYY-MM-DD" and times "HH:mm", like the availability search. */
export type ReservationSlot = {
  workspace: SlotWorkspace;
  date: string;
  from: string;
  to: string;
};

/** "13:00:00" (how the API sends TimeOnly) → "13:00" (how the UI shows and searches times). */
export const shortTime = (time: string) => time.slice(0, 5);

/** Body of POST /api/reservations/mine. The API reads times as "HH:mm:ss". */
export function toCreateRequest(slot: ReservationSlot): CreateReservationRequest {
  return {
    workspaceId: slot.workspace.id,
    date: slot.date,
    from: `${slot.from}:00`,
    to: `${slot.to}:00`,
  };
}

/** Query of GET /api/reservations/alternatives for a slot that turned out to be taken. */
export function toAlternativesQuery(slot: ReservationSlot): AlternativesQuery {
  return { WorkspaceId: slot.workspace.id, Date: slot.date, From: slot.from, To: slot.to };
}

/** UC06 offer "another workspace, same time" → the slot to reserve. */
export function slotWithWorkspace(
  slot: ReservationSlot,
  other: AlternativeWorkspace,
): ReservationSlot {
  return {
    ...slot,
    workspace: {
      id: other.id,
      code: other.code,
      type: other.type,
      capacity: other.capacity,
      roomName: other.roomName,
    },
  };
}

/** UC06 offer "same workspace, another time" → the slot to reserve. */
export function slotAtTime(slot: ReservationSlot, other: AlternativeSlot): ReservationSlot {
  return { ...slot, date: other.date, from: shortTime(other.from), to: shortTime(other.to) };
}
