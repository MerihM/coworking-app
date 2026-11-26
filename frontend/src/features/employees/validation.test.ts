import { describe, expect, it } from "vitest";
import i18n from "../../i18n";
import { createEmployeeSchema, resetPasswordSchema, updateEmployeeSchema } from "./validation";

// Same rules as the backend validators (CreateEmployeeRequestValidator...).
const t = i18n.t.bind(i18n);

const valid = {
  firstName: "Emir",
  lastName: "Kovač",
  email: "emir@coworking.ba",
  role: "Receptionist" as const,
  password: "Coworking123",
};

function failing(schema: ReturnType<typeof createEmployeeSchema>, values: object): string[] {
  const result = schema.safeParse({ ...valid, ...values });
  // One entry per field, even when a field breaks several rules ("short": length AND digit).
  return result.success
    ? []
    : [...new Set(result.error.issues.map((issue) => issue.path.join(".")))];
}

describe("employee schemas", () => {
  it("accepts a valid new employee", () => {
    expect(failing(createEmployeeSchema(t), {})).toEqual([]);
  });

  it("rejects a role the API doesn't know (members are not staff)", () => {
    expect(failing(createEmployeeSchema(t), { role: "Member" })).toEqual(["role"]);
  });

  it("checks the initial password when creating", () => {
    expect(failing(createEmployeeSchema(t), { password: "short" })).toEqual(["password"]);
  });

  it("ignores the password when editing", () => {
    expect(failing(updateEmployeeSchema(t), { password: "" })).toEqual([]);
  });

  it("requires names and a valid e-mail", () => {
    expect(failing(updateEmployeeSchema(t), { firstName: " ", email: "not-an-email" })).toEqual([
      "firstName",
      "email",
    ]);
  });
});

describe("reset password schema", () => {
  const schema = resetPasswordSchema(t);

  it("accepts matching passwords", () => {
    expect(
      schema.safeParse({ newPassword: "Coworking123", confirmPassword: "Coworking123" }).success,
    ).toBe(true);
  });

  it("reports different passwords on the repeat field", () => {
    const result = schema.safeParse({
      newPassword: "Coworking123",
      confirmPassword: "Coworking124",
    });
    expect(result.success).toBe(false);
    expect(result.error?.issues.map((issue) => issue.path.join("."))).toEqual(["confirmPassword"]);
  });
});
