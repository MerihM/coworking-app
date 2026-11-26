// The result type every feature API wrapper returns (employees, rooms, workspaces...), and the one
// helper that produces it. Moved here from employeesApi.ts in CW-23, when the second API arrived.
import { toProblem, type ApiProblem } from "./problem";

/** Success with the response body, or failure with the parsed ProblemDetails. Never throws. */
export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: ApiProblem };

/**
 * Runs one openapi-fetch call and turns its answer into ApiResult. A request that never got an
 * answer (API down, offline) becomes status 0, which problemMessage() shows as "server unreachable".
 */
export async function callApi<T>(
  send: () => Promise<{ data?: unknown; error?: unknown; response: Response }>,
): Promise<ApiResult<T>> {
  try {
    const { data, error, response } = await send();
    return response.ok
      ? { ok: true, data: data as T }
      : { ok: false, problem: toProblem(error, response) };
  } catch {
    return { ok: false, problem: { status: 0 } };
  }
}
