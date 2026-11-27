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
  "membership_types.name_taken": "errors.membershipTypeNameTaken",
  "availability.in_past": "errors.inPast",
  "members.inactive": "errors.memberInactive",
  "members.fixed_desk_invalid": "errors.fixedDeskInvalid",
  "members.fixed_desk_taken": "errors.fixedDeskTaken",
  "memberships.type_inactive": "errors.membershipTypeInactive",
  "memberships.unpaid_exists": "errors.unpaidExists",
  "memberships.already_paid": "errors.alreadyPaid",
  // CW-29/30: reservations. A past start uses the same sentence as the search.
  "reservations.workspace_unavailable": "errors.workspaceUnavailable",
  "reservations.in_past": "errors.inPast",
  "reservations.slot_taken": "errors.slotTaken",
  "reservations.not_cancellable": "errors.notCancellable",
} as const;

/**
 * Business rule code → translation key, for errors that come with `ruleCode` but no `code`
 * (domain rules such as BR04, CW-25). Their English `detail` is written for developers, so the
 * UI shows its own translated sentence instead.
 */
const ruleMessageKeys = {
  // CW-30: no active membership on the reservation date (the UI also offers buying one).
  BR02: "errors.noActiveMembership",
  // CW-31: a member cancelling less than 2 hours before the start.
  BR03: "errors.cancelTooLate",
  BR04: "errors.outsideWorkingHours",
  // CW-26: a payment whose amount differs from the membership price.
  BR05: "errors.paymentAmount",
} as const;

type KnownCode = keyof typeof codeMessageKeys;

function isKnownCode(code: string | undefined): code is KnownCode {
  return code !== undefined && Object.hasOwn(codeMessageKeys, code);
}

type KnownRule = keyof typeof ruleMessageKeys;

function isKnownRule(ruleCode: string | undefined): ruleCode is KnownRule {
  return ruleCode !== undefined && Object.hasOwn(ruleMessageKeys, ruleCode);
}

/** One sentence for an alert or notification. */
export function problemMessage(problem: ApiProblem, t: TFunction): string {
  if (isKnownCode(problem.code)) {
    return t(codeMessageKeys[problem.code]);
  }
  if (isKnownRule(problem.ruleCode)) {
    return t(ruleMessageKeys[problem.ruleCode]);
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
