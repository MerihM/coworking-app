// Small builders for test data, so each test shows only what matters to it.
import type { Session, CurrentUser } from "../features/auth/sessionStore";

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
