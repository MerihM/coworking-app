import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { jsonResponse, testSession } from "../../test/fixtures";
import { refreshSession } from "./refresh";
import { sessionStore } from "./sessionStore";

describe("refreshSession", () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    sessionStore.set(null);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    Reflect.deleteProperty(navigator, "locks");
  });

  it("sends only ONE request when called several times at once (single-flight)", async () => {
    fetchMock.mockResolvedValue(jsonResponse(200, testSession("token-1")));

    const [first, second, third] = await Promise.all([
      refreshSession(),
      refreshSession(),
      refreshSession(),
    ]);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(first).toBe(second);
    expect(second).toBe(third);
    expect(sessionStore.get()?.accessToken).toBe("token-1");
  });

  it("starts a new request after the previous one finished", async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, testSession("token-1")))
      .mockResolvedValueOnce(jsonResponse(200, testSession("token-2")));

    await refreshSession();
    await refreshSession();

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(sessionStore.get()?.accessToken).toBe("token-2");
  });

  it("clears the session when the server says the refresh token is invalid", async () => {
    sessionStore.set(testSession("old"));
    fetchMock.mockResolvedValue(jsonResponse(401, { status: 401, code: "auth.refresh_invalid" }));

    const result = await refreshSession();

    expect(result).toBeNull();
    expect(sessionStore.get()).toBeNull();
  });

  it("keeps the session when the API can't be reached at all", async () => {
    sessionStore.set(testSession("old"));
    fetchMock.mockRejectedValue(new TypeError("Failed to fetch"));

    const result = await refreshSession();

    expect(result).toBeNull();
    expect(sessionStore.get()?.accessToken).toBe("old");
  });

  it("runs inside the cross-tab lock when the browser supports Web Locks", async () => {
    const request = vi.fn((_name: string, work: () => Promise<unknown>) => work());
    Object.defineProperty(navigator, "locks", { configurable: true, value: { request } });
    fetchMock.mockResolvedValue(jsonResponse(200, testSession("token-1")));

    await refreshSession();

    expect(request).toHaveBeenCalledWith("cw-refresh", expect.any(Function));
  });
});
