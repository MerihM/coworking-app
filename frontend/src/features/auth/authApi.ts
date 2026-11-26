// The login/register/logout calls. They update sessionStore, so every component using useAuth()
// re-renders with the new state.
import { api } from "../../api/client";
import { toProblem, type ApiProblem } from "../../api/problem";
import type { components } from "../../api/schema";
import { sessionStore } from "./sessionStore";

export type LoginRequest = components["schemas"]["LoginRequest"];
export type RegisterMemberRequest = components["schemas"]["RegisterMemberRequest"];

/** Result for forms: either success, or the ProblemDetails to show (field errors, code, 429...). */
export type AuthResult = { ok: true } | { ok: false; problem: ApiProblem };

export async function login(body: LoginRequest): Promise<AuthResult> {
  const { data, error, response } = await api.POST("/api/auth/login", { body });
  if (data) {
    sessionStore.set(data);
    return { ok: true };
  }
  return { ok: false, problem: toProblem(error, response) };
}

export async function register(body: RegisterMemberRequest): Promise<AuthResult> {
  const { data, error, response } = await api.POST("/api/auth/register", { body });
  if (data) {
    sessionStore.set(data);
    return { ok: true };
  }
  return { ok: false, problem: toProblem(error, response) };
}

/** Revokes the session on the server and forgets it here – even if the server can't be reached. */
export async function logout(): Promise<void> {
  try {
    await api.POST("/api/auth/logout");
  } finally {
    sessionStore.set(null);
  }
}
