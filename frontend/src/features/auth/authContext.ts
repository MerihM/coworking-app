// The auth state as React context. Kept in its own file (without components) so React Fast
// Refresh keeps working for AuthProvider.tsx, and so tests can provide a fake value directly.
import { createContext } from "react";
import type { AuthResult, LoginRequest, RegisterMemberRequest } from "./authApi";
import type { CurrentUser } from "./sessionStore";

/**
 * - loading: the app just started and is asking /api/auth/refresh whether a session exists
 * - authenticated: a user is logged in
 * - anonymous: nobody is logged in (Visitor)
 */
export type AuthStatus = "loading" | "authenticated" | "anonymous";

export type AuthContextValue = {
  status: AuthStatus;
  user: CurrentUser | null;
  login: (body: LoginRequest) => Promise<AuthResult>;
  register: (body: RegisterMemberRequest) => Promise<AuthResult>;
  logout: () => Promise<void>;
};

export const AuthContext = createContext<AuthContextValue | null>(null);
