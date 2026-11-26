import { describe, expect, it } from "vitest";
import i18n from "../../i18n";
import { roomSchema } from "./validation";

// Same rules as the backend validators (CreateRoomRequestValidator...).
const t = i18n.t.bind(i18n);

const valid = { name: "Open Space", floor: 1, description: "" };

describe("roomSchema", () => {
  const schema = roomSchema(t);

  it("accepts a normal room, also on negative floors", () => {
    expect(schema.safeParse(valid).success).toBe(true);
    expect(schema.safeParse({ ...valid, floor: -5 }).success).toBe(true);
  });

  it.each(["", "  ", " A "])(
    "rejects the name %j (at least 2 characters after trimming)",
    (name) => {
      expect(schema.safeParse({ ...valid, name }).success).toBe(false);
    },
  );

  it.each([-6, 101, 1.5])("rejects floor %s", (floor) => {
    expect(schema.safeParse({ ...valid, floor }).success).toBe(false);
  });

  it("rejects an empty floor field (NumberInput gives an empty string)", () => {
    expect(schema.safeParse({ ...valid, floor: "" }).success).toBe(false);
  });

  it("rejects a description over 1000 characters", () => {
    expect(schema.safeParse({ ...valid, description: "x".repeat(1001) }).success).toBe(false);
  });
});
