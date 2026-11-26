// Form rules for rooms – the same as CreateRoomRequestValidator / UpdateRoomRequestValidator.
import type { TFunction } from "i18next";
import { z } from "zod";

/** Same limits as the backend (RoomRuleExtensions) and the database columns. */
export const roomLimits = {
  nameMin: 2,
  nameMax: 100,
  floorMin: -5,
  floorMax: 100,
  descriptionMax: 1000,
} as const;

/** Create and edit use the same fields and rules. */
export function roomSchema(t: TFunction) {
  return z.object({
    name: z
      .string()
      .trim()
      .min(1, { error: t("validation.required") })
      .min(roomLimits.nameMin, { error: t("validation.tooShort", { min: roomLimits.nameMin }) })
      .max(roomLimits.nameMax, { error: t("validation.tooLong", { max: roomLimits.nameMax }) }),
    // NumberInput gives "" when the field is emptied: that is not a number → "required".
    floor: z
      .number({ error: t("validation.required") })
      .int({ error: t("validation.wholeNumber") })
      .min(roomLimits.floorMin, {
        error: t("validation.between", { min: roomLimits.floorMin, max: roomLimits.floorMax }),
      })
      .max(roomLimits.floorMax, {
        error: t("validation.between", { min: roomLimits.floorMin, max: roomLimits.floorMax }),
      }),
    description: z
      .string()
      .trim()
      .max(roomLimits.descriptionMax, {
        error: t("validation.tooLong", { max: roomLimits.descriptionMax }),
      }),
  });
}

export type RoomFormValues = z.input<ReturnType<typeof roomSchema>>;
