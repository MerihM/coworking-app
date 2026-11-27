import { describe, expect, it } from "vitest";
import i18n from "../i18n";
import { fieldErrors, problemMessage } from "./problemMessages";

const t = i18n.t.bind(i18n);

describe("problemMessage", () => {
  it("translates a known error code", () => {
    expect(problemMessage({ status: 401, code: "auth.invalid_credentials" }, t)).toBe(
      "Wrong e-mail or password.",
    );
  });

  it("follows the current language", async () => {
    await i18n.changeLanguage("bs");
    try {
      expect(problemMessage({ status: 409, code: "auth.email_taken" }, t)).toBe(
        "Račun sa ovom e-mail adresom već postoji.",
      );
    } finally {
      await i18n.changeLanguage("en");
    }
  });

  it("translates the membership codes and the BR05 payment rule (CW-26)", () => {
    expect(problemMessage({ status: 409, code: "memberships.unpaid_exists" }, t)).toBe(
      "There is already an unpaid membership waiting for payment. Pay or cancel it first.",
    );
    expect(problemMessage({ status: 422, ruleCode: "BR05", detail: "developer text" }, t)).toBe(
      "The amount must be exactly the membership price.",
    );
  });

  it.each([
    [{ status: 0 }, "The server can't be reached. Check your connection and try again."],
    [{ status: 429 }, "Too many attempts. Wait a minute and try again."],
    [{ status: 500 }, "Something went wrong on the server. Please try again later."],
    [{ status: 418, detail: "I'm a teapot." }, "I'm a teapot."],
  ])("falls back by status/detail: %o", (problem, expected) => {
    expect(problemMessage(problem, t)).toBe(expected);
  });
});

describe("fieldErrors", () => {
  it("takes the first message of each field", () => {
    expect(
      fieldErrors({
        status: 400,
        errors: { email: ["first", "second"], password: ["too short"], empty: [] },
      }),
    ).toEqual({ email: "first", password: "too short" });
  });
});
