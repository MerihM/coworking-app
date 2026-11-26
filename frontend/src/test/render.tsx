// Renders a component with everything the app normally provides around it:
// Mantine theme, a fake auth state and a router (for <Link>, useSearchParams...).
import { MantineProvider } from "@mantine/core";
import { render } from "@testing-library/react";
import type { ReactElement } from "react";
import { createMemoryRouter, RouterProvider } from "react-router";
import type { AuthContextValue } from "../features/auth/authContext";
import { AuthContext } from "../features/auth/authContext";

export function fakeAuth(overrides: Partial<AuthContextValue> = {}): AuthContextValue {
  return {
    status: "anonymous",
    user: null,
    login: async () => ({ ok: true }),
    register: async () => ({ ok: true }),
    logout: async () => undefined,
    ...overrides,
  };
}

/**
 * Renders `ui` as the page at `path`.
 * `routePath` is the route PATTERN – pass e.g. "/manage/rooms/:roomId" when the page reads
 * useParams() (CW-23); the default "*" matches any path but has no parameters.
 */
export function renderPage(
  ui: ReactElement,
  auth: AuthContextValue = fakeAuth(),
  path = "/",
  routePath = "*",
) {
  const router = createMemoryRouter([{ path: routePath, element: ui }], {
    initialEntries: [path],
  });

  return render(
    // env="test": no transitions/portals timing tricks, so modals and menus render at once in jsdom.
    <MantineProvider env="test">
      <AuthContext.Provider value={auth}>
        <RouterProvider router={router} />
      </AuthContext.Provider>
    </MantineProvider>,
  );
}
