// Turns an API error (ProblemDetails) into text for the user, in the current language.
//
// The backend sends a stable `code` for every expected error (CW-11/12); this file maps each code
// to a translation key. Field validation messages ("errors") come already translated from the
// backend (Accept-Language), so forms show them as they are.
import type { TFunction } from "i18next";
import type { ApiProblem } from "./problem";

/** Backend error code → translation key. Add a line here when the backend adds a code. */
const codeMessageKeys = {
  "auth.invalid_credentials": "errors.invalidCredentials",
  "auth.email_taken": "errors.emailTaken",
  "auth.too_many_requests": "errors.tooManyRequests",
  "auth.refresh_invalid": "errors.sessionExpired",
  "auth.unauthenticated": "errors.sessionExpired",
  "auth.forbidden": "errors.forbidden",
  "conflict.duplicate": "errors.duplicate",
  "conflict.in_use": "errors.inUse",
  "employees.self_change": "errors.selfChange",
  "employees.last_admin": "errors.lastAdmin",
  "rooms.name_taken": "errors.roomNameTaken",
  "workspaces.code_taken": "errors.workspaceCodeTaken",
  "workspaces.fixed_assigned": "errors.fixedDeskAssigned",
  "workspaces.room_inactive": "errors.roomInactive",
} as const;

type KnownCode = keyof typeof codeMessageKeys;

function isKnownCode(code: string | undefined): code is KnownCode {
  return code !== undefined && Object.hasOwn(codeMessageKeys, code);
}

/** One sentence for an alert or notification. */
export function problemMessage(problem: ApiProblem, t: TFunction): string {
  if (isKnownCode(problem.code)) {
    return t(codeMessageKeys[problem.code]);
  }
  if (problem.status === 0) {
    return t("errors.network");
  }
  if (problem.status === 429) {
    return t("errors.tooManyRequests");
  }
  if (problem.status >= 500) {
    return t("errors.server");
  }
  // Unknown code: the English detail from the API is better than nothing.
  return problem.detail ?? t("errors.unknown");
}

/**
 * Field errors from a 400 response, in the shape @mantine/form's setErrors() expects:
 * { email: "first message", password: "first message" }. Keys are already camelCase (CW-11),
 * exactly the form field names.
 */
export function fieldErrors(problem: ApiProblem): Record<string, string> {
  return Object.fromEntries(
    Object.entries(problem.errors ?? {})
      .filter(([, messages]) => messages.length > 0)
      .map(([field, messages]) => [field, messages[0]]),
  );
}
