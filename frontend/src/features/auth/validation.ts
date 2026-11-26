// Form rules for login and registration (zod v4 schemas, used by @mantine/form's schemaResolver).
//
// They MIRROR the backend validators (RegisterMemberRequestValidator, LoginRequestValidator,
// PasswordRules) so the user gets instant feedback while typing. The backend still checks
// everything again – the browser can be bypassed, the API can't.
//
// The schemas are built with `t`, so messages follow the language switch. The field builders
// (textField, emailField, passwordField) are shared with the staff forms (CW-17).
import type { TFunction } from "i18next";
import { z } from "zod";

/** Same limits as the backend (and the database columns). */
export const limits = {
  nameMax: 100,
  emailMax: 256,
  phoneMax: 30,
  passwordMin: 8,
  passwordMax: 128,
} as const;

/** Same regex as RegisterMemberRequestValidator: "+387 61 123 456", "061/123-456", "(033) 123 456". */
export const phonePattern = /^\+?\(?[0-9][0-9 ()/-]{5,29}$/;

/** Required text (spaces around it don't count) with a maximum length. */
export function textField(t: TFunction, max: number) {
  return z
    .string()
    .trim()
    .min(1, { error: t("validation.required") })
    .max(max, { error: t("validation.tooLong", { max }) });
}

/** Required, valid e-mail address. */
export function emailField(t: TFunction) {
  return textField(t, limits.emailMax).pipe(z.email({ error: t("validation.email") }));
}

/** Password policy: 8–128 characters, at least one letter (any alphabet, incl. č ć š ž đ) and one digit. */
export function passwordField(t: TFunction) {
  return z
    .string()
    .min(limits.passwordMin, { error: t("validation.passwordLength", { min: limits.passwordMin }) })
    .max(limits.passwordMax, { error: t("validation.tooLong", { max: limits.passwordMax }) })
    .regex(/\p{L}/u, { error: t("validation.passwordLetter") })
    .regex(/[0-9]/, { error: t("validation.passwordDigit") });
}

/** Login: only "present" – an old password that breaks today's policy must still work. */
export function createLoginSchema(t: TFunction) {
  return z.object({
    email: textField(t, limits.emailMax),
    password: z
      .string()
      .min(1, { error: t("validation.required") })
      .max(limits.passwordMax, { error: t("validation.tooLong", { max: limits.passwordMax }) }),
  });
}

/** Registration (FR01): name, surname, e-mail, phone, password + repeated password. */
export function createRegisterSchema(t: TFunction) {
  return z
    .object({
      firstName: textField(t, limits.nameMax),
      lastName: textField(t, limits.nameMax),
      email: emailField(t),
      phone: textField(t, limits.phoneMax).regex(phonePattern, { error: t("validation.phone") }),
      password: passwordField(t),
      // Only in the browser: catches typos in the password before it is sent.
      confirmPassword: z.string(),
    })
    .refine((values) => values.password === values.confirmPassword, {
      error: t("validation.passwordsDiffer"),
      path: ["confirmPassword"],
    });
}

export type LoginFormValues = z.input<ReturnType<typeof createLoginSchema>>;
export type RegisterFormValues = z.input<ReturnType<typeof createRegisterSchema>>;
