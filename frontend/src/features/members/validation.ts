// Form rules for members created/edited at the reception – the same as CreateMemberRequestValidator
// and UpdateMemberRequestValidator on the backend (which repeat the registration rules, FR01).
import type { TFunction } from "i18next";
import { z } from "zod";
import { emailField, limits, passwordField, phonePattern, textField } from "../auth/validation";

/** Fields both forms share. `fixedDeskId` is the Select's value: a desk id as text, or null. */
function memberFields(t: TFunction) {
  return {
    firstName: textField(t, limits.nameMax),
    lastName: textField(t, limits.nameMax),
    email: emailField(t),
    phone: textField(t, limits.phoneMax).regex(phonePattern, { error: t("validation.phone") }),
    fixedDeskId: z.string().nullable(),
  };
}

/** New member: all fields, including the initial password. */
export function createMemberSchema(t: TFunction) {
  return z.object({ ...memberFields(t), password: passwordField(t) });
}

/**
 * Edit: the same fields WITHOUT a password rule (members change their own password).
 * `password` stays in the shape so both modes share one form type; its value is ignored.
 */
export function updateMemberSchema(t: TFunction) {
  return z.object({ ...memberFields(t), password: z.string() });
}

export type MemberFormValues = z.input<ReturnType<typeof createMemberSchema>>;
