import { useContext } from "react";
import { AuthContext, type AuthContextValue } from "./authContext";

/** Who is logged in, and the login/register/logout actions. Use inside <AuthProvider>. */
export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error("useAuth() must be used inside <AuthProvider>.");
  }
  return value;
}
