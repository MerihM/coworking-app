// Form rules for membership types – the same as MembershipTypeInputValidator on the backend.
import type { TFunction } from "i18next";
import { z } from "zod";

/** Same limits as the backend (MembershipTypeInputValidator) and the name column. */
export const membershipTypeLimits = {
  nameMin: 2,
  nameMax: 100,
  durationMin: 1,
  durationMax: 365,
  priceMin: 0.01,
  priceMax: 10_000,
} as const;

/**
 * At most two decimals. Checked on the number's text ("15.5", "15.25"), not with
 * `price * 100`, because binary floating point makes 15.1 * 100 = 1510.0000000000002.
 */
const twoDecimals = (value: number) => /^\d+(\.\d{1,2})?$/.test(String(value));

/** Create and edit use the same fields and rules. */
export function membershipTypeSchema(t: TFunction) {
  const l = membershipTypeLimits;
  return z.object({
    name: z
      .string()
      .trim()
      .min(1, { error: t("validation.required") })
      .min(l.nameMin, { error: t("validation.tooShort", { min: l.nameMin }) })
      .max(l.nameMax, { error: t("validation.tooLong", { max: l.nameMax }) }),
    durationDays: z
      .number({ error: t("validation.required") })
      .int({ error: t("validation.wholeNumber") })
      .min(l.durationMin, {
        error: t("validation.between", { min: l.durationMin, max: l.durationMax }),
      })
      .max(l.durationMax, {
        error: t("validation.between", { min: l.durationMin, max: l.durationMax }),
      }),
    price: z
      .number({ error: t("validation.required") })
      .min(l.priceMin, { error: t("membershipTypes.validation.price") })
      .max(l.priceMax, { error: t("membershipTypes.validation.price") })
      .refine(twoDecimals, { error: t("membershipTypes.validation.priceDecimals") }),
  });
}

export type MembershipTypeFormValues = z.input<ReturnType<typeof membershipTypeSchema>>;
