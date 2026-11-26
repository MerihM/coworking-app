import { describe, expect, it } from "vitest";
import i18n from "../../i18n";
import { createLoginSchema, createRegisterSchema } from "./validation";

// The same cases as the backend tests (RegisterMemberRequestValidatorTests), so browser and API
// agree on what is valid.
const t = i18n.t.bind(i18n);
const register = createRegisterSchema(t);

const valid = {
  firstName: "Amra",
  lastName: "Hodžić",
  email: "amra@example.com",
  phone: "+387 61 123 456",
  password: "Coworking123",
  confirmPassword: "Coworking123",
};

/** Field names that have an error for these values. */
function failingFields(values: Partial<typeof valid>): string[] {
  const result = register.safeParse({ ...valid, ...values });
  return result.success ? [] : result.error.issues.map((issue) => issue.path.join("."));
}

describe("register schema", () => {
  it("accepts a valid registration", () => {
    expect(failingFields({})).toEqual([]);
  });

  it.each(["short1", "abcdefgh", "12345678", "a".repeat(128) + "1"])(
    "rejects the password %s",
    (password) => {
      expect(failingFields({ password, confirmPassword: password })).toContain("password");
    },
  );

  it.each(["abcdefg1", "šifra2024"])("accepts the password %s", (password) => {
    expect(failingFields({ password, confirmPassword: password })).toEqual([]);
  });

  it.each(["+387 61 123 456", "061/123-456", "(033) 123 456"])("accepts the phone %s", (phone) => {
    expect(failingFields({ phone })).toEqual([]);
  });

  it.each(["", "12345", "call me", "+387 61 123 456 ext. 2"])("rejects the phone %s", (phone) => {
    expect(failingFields({ phone })).toContain("phone");
  });

  it("rejects an invalid e-mail and too long names", () => {
    const fields = failingFields({ email: "nope", firstName: "a".repeat(101) });

    expect(fields).toContain("email");
    expect(fields).toContain("firstName");
  });

  it("reports different passwords on the confirmation field", () => {
    expect(failingFields({ confirmPassword: "Something-else-1" })).toEqual(["confirmPassword"]);
  });
});

describe("login schema", () => {
  it("accepts an old password that breaks today's policy", () => {
    expect(createLoginSchema(t).safeParse({ email: "a@b.c", password: "abc" }).success).toBe(true);
  });

  it("requires both fields", () => {
    const result = createLoginSchema(t).safeParse({ email: " ", password: "" });

    expect(result.success).toBe(false);
  });
});
