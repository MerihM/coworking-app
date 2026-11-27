// Small builders for test data, so each test shows only what matters to it.
import type { Session, CurrentUser } from "../features/auth/sessionStore";
import type { Reservation } from "../features/reservations/reservationsApi";

export function testUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    id: 1,
    userType: "member",
    role: "Member",
    email: "amra@example.com",
    fullName: "Amra Hodžić",
    ...overrides,
  };
}

export function testSession(accessToken: string, user: CurrentUser = testUser()): Session {
  return {
    accessToken,
    accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
    user,
  };
}

export function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

/** A confirmed upcoming reservation of OS-01 (CW-31); override what the test is about. */
export function testReservation(overrides: Partial<Reservation> = {}): Reservation {
  return {
    id: 100,
    memberId: 1,
    memberName: "Amra Hodžić",
    memberEmail: "amra@example.com",
    workspaceId: 7,
    workspaceCode: "OS-01",
    workspaceType: "HotDesk",
    capacity: 1,
    roomName: "Open Space",
    startsAt: "2026-11-30T12:00:00Z",
    endsAt: "2026-11-30T16:00:00Z",
    date: "2026-11-30",
    from: "13:00:00",
    to: "17:00:00",
    status: "Confirmed",
    createdAt: "2026-11-27T10:00:00Z",
    cancelledAt: null,
    cancelledByName: null,
    canMemberCancel: true,
    ...overrides,
  };
}
