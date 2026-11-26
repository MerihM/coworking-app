import { Navigate, Outlet, useSearchParams } from "react-router";
import { FullScreenLoader } from "../../components/FullScreenLoader";
import { safeRedirectTarget } from "./redirect";
import { useAuth } from "./useAuth";

/**
 * Route guard for /login and /register: a logged-in user doesn't need them. As soon as the user
 * is authenticated – also right after a successful login on this very page – they are sent to
 * the page they originally wanted (?from=...) or to the start page.
 */
export function GuestOnly() {
  const { status } = useAuth();
  const [searchParams] = useSearchParams();

  if (status === "loading") {
    return <FullScreenLoader />;
  }

  if (status === "authenticated") {
    return <Navigate to={safeRedirectTarget(searchParams.get("from"))} replace />;
  }

  return <Outlet />;
}
