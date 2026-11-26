import { describe, expect, it } from "vitest";
import i18n from "../../i18n";
import { membershipTypeSchema } from "./validation";

// Same rules as the backend (MembershipTypeInputValidator).
const t = i18n.t.bind(i18n);
const schema = membershipTypeSchema(t);
const valid = { name: "Monthly", durationDays: 30, price: 180 };

function fieldsWithErrors(values: object): string[] {
  const result = schema.safeParse({ ...valid, ...values });
  return result.success ? [] : [...new Set(result.error.issues.map((i) => i.path.join(".")))];
}

describe("membershipTypeSchema", () => {
  it.each([0.01, 15, 15.5, 15.25, 175.5, 10000])("accepts the price %s", (price) => {
    expect(fieldsWithErrors({ price })).toEqual([]);
  });

  it.each([0, -1, 10000.01, 15.505, 15.123])("rejects the price %s", (price) => {
    expect(fieldsWithErrors({ price })).toEqual(["price"]);
  });

  it("explains too many decimals", () => {
    const result = schema.safeParse({ ...valid, price: 15.505 });
    expect(result.success ? "" : result.error.issues[0].message).toBe(
      "At most 2 decimals (e.g. 15.50).",
    );
  });

  it.each([1, 7, 30, 365])("accepts %i days", (durationDays) => {
    expect(fieldsWithErrors({ durationDays })).toEqual([]);
  });

  it.each([0, 366, 1.5])("rejects %s days", (durationDays) => {
    expect(fieldsWithErrors({ durationDays })).toEqual(["durationDays"]);
  });

  it.each(["", " A "])("rejects the name %j", (name) => {
    expect(fieldsWithErrors({ name })).toEqual(["name"]);
  });
});
