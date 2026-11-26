import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { sessionStore } from "../features/auth/sessionStore";
import { jsonResponse, testSession, testUser } from "../test/fixtures";

// openapi-fetch remembers globalThis.fetch when the client is CREATED (on import). vi.hoisted runs
// before the imports, so the client below picks up this mock.
const fetchMock = vi.hoisted(() => {
  const mock = vi.fn<typeof fetch>();
  globalThis.fetch = mock;
  return mock;
});

const { api } = await import("./client");

/** The path of a fetch call, whether it was made with a Request object or a URL string. */
function pathOf(input: Parameters<typeof fetch>[0]): string {
  const url = input instanceof Request ? input.url : input.toString();
  return new URL(url, window.location.origin).pathname;
}

describe("api client middleware", () => {
  beforeEach(() => {
    fetchMock.mockReset();
    sessionStore.set(null);
  });

  afterEach(() => sessionStore.set(null));

  it("sends the access token and the UI language", async () => {
    sessionStore.set(testSession("token-1"));
    fetchMock.mockResolvedValue(jsonResponse(200, testUser()));

    await api.GET("/api/auth/me");

    const request = fetchMock.mock.calls[0][0] as Request;
    expect(request.headers.get("Authorization")).toBe("Bearer token-1");
    expect(request.headers.get("Accept-Language")).toBe("en");
  });

  it("on 401 refreshes once and repeats the request with the new token", async () => {
    sessionStore.set(testSession("expired"));
    fetchMock.mockImplementation(async (input) => {
      if (pathOf(input) === "/api/auth/refresh") {
        return jsonResponse(200, testSession("fresh"));
      }
      const authorization = (input as Request).headers.get("Authorization");
      return authorization === "Bearer fresh"
        ? jsonResponse(200, testUser({ fullName: "After Refresh" }))
        : jsonResponse(401, { status: 401, code: "auth.unauthenticated" });
    });

    const { data, error } = await api.GET("/api/auth/me");

    expect(error).toBeUndefined();
    expect(data?.fullName).toBe("After Refresh");
    expect(fetchMock.mock.calls.map(([input]) => pathOf(input))).toEqual([
      "/api/auth/me", // 401
      "/api/auth/refresh", // new token
      "/api/auth/me", // repeated, 200
    ]);
  });

  it("returns the 401 and logs out when the refresh fails", async () => {
    sessionStore.set(testSession("expired"));
    fetchMock.mockImplementation(async (input) =>
      pathOf(input) === "/api/auth/refresh"
        ? jsonResponse(401, { status: 401, code: "auth.refresh_invalid" })
        : jsonResponse(401, { status: 401, code: "auth.unauthenticated" }),
    );

    const { response } = await api.GET("/api/auth/me");

    expect(response.status).toBe(401);
    expect(sessionStore.get()).toBeNull();
  });

  it("does not refresh after a failed LOGIN (wrong password is not an expired token)", async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(401, { status: 401, code: "auth.invalid_credentials" }),
    );

    await api.POST("/api/auth/login", { body: { email: "a@example.com", password: "wrong" } });

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
