import { describe, expect, it } from "vitest";
import { safeRedirectTarget } from "./redirect";

describe("safeRedirectTarget", () => {
  it.each([
    ["/account", "/account"],
    ["/admin/staff?page=2", "/admin/staff?page=2"],
  ])("keeps the in-app path %s", (from, expected) => {
    expect(safeRedirectTarget(from)).toBe(expected);
  });

  it.each([
    null,
    undefined,
    "",
    "https://evil.example",
    "//evil.example",
    "/\\evil.example",
    "javascript:alert(1)",
  ])("sends %s to the start page (open redirect protection)", (from) => {
    expect(safeRedirectTarget(from)).toBe("/");
  });
});
