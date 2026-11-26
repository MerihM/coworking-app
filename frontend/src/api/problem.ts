// The shape of every error response from the API (RFC 7807 ProblemDetails, CW-5/CW-11).
// The generated schema only knows the standard fields; "code" and "errors" are our extensions.

export type ApiProblem = {
  /** HTTP status, e.g. 401. Always set (taken from the response if the body has none). */
  status: number;
  title?: string;
  /** English explanation – a fallback only; show t(`errors.${code}`) when a code is present. */
  detail?: string;
  /** Stable error code for translation, e.g. "auth.invalid_credentials". */
  code?: string;
  /** Validation errors per field (camelCase keys): { "email": ["..."] }. */
  errors?: Record<string, string[]>;
  /** Business rule from the design document, e.g. "BR04". */
  ruleCode?: string;
};

/** Normalizes an openapi-fetch `error` value (parsed JSON, text or undefined) into ApiProblem. */
export function toProblem(error: unknown, response: Response): ApiProblem {
  const body = typeof error === "object" && error !== null ? (error as Partial<ApiProblem>) : {};
  return { ...body, status: body.status ?? response.status };
}
