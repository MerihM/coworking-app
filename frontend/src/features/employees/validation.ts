// Form rules for staff accounts – the same as CreateEmployeeRequestValidator,
// UpdateEmployeeRequestValidator and ResetPasswordRequestValidator on the backend.
import type { TFunction } from "i18next";
import { z } from "zod";
import { emailField, limits, passwordField, textField } from "../auth/validation";
import { employeeRoles } from "./employeesApi";

function role(t: TFunction) {
  return z.enum(employeeRoles, { error: t("validation.required") });
}

/** New account: all fields, including the initial password. */
export function createEmployeeSchema(t: TFunction) {
  return z.object({
    firstName: textField(t, limits.nameMax),
    lastName: textField(t, limits.nameMax),
    email: emailField(t),
    role: role(t),
    password: passwordField(t),
  });
}

/**
 * Edit: the same fields WITHOUT a password rule (the password has its own reset dialog).
 * `password` stays in the shape so both modes can share one form type; its value is ignored.
 */
export function updateEmployeeSchema(t: TFunction) {
  return z.object({
    firstName: textField(t, limits.nameMax),
    lastName: textField(t, limits.nameMax),
    email: emailField(t),
    role: role(t),
    password: z.string(),
  });
}

/** Reset password: the policy plus a repeated password against typos. */
export function resetPasswordSchema(t: TFunction) {
  return z
    .object({
      newPassword: passwordField(t),
      confirmPassword: z.string(),
    })
    .refine((values) => values.newPassword === values.confirmPassword, {
      error: t("validation.passwordsDiffer"),
      path: ["confirmPassword"],
    });
}

export type EmployeeFormValues = z.input<ReturnType<typeof createEmployeeSchema>>;
export type ResetPasswordFormValues = z.input<ReturnType<typeof resetPasswordSchema>>;
