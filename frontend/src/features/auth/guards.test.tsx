import { MantineProvider } from "@mantine/core";
import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it } from "vitest";
import { testUser } from "../../test/fixtures";
import { AuthContext, type AuthContextValue } from "./authContext";
import { GuestOnly } from "./GuestOnly";
import { RequireAuth } from "./RequireAuth";
import { RequireRole } from "./RequireRole";
import { Roles } from "./roles";

/** Renders a small route tree with the guards, at `path`, with a fake auth state. */
function renderAt(path: string, auth: Pick<AuthContextValue, "status" | "user">) {
  const router = createMemoryRouter(
    [
      { path: "/", element: <p>home page</p> },
      { path: "/login", element: <p>login page</p> },
      { path: "/forbidden", element: <p>forbidden page</p> },
      {
        Component: RequireAuth,
        children: [
          { path: "/account", element: <p>account page</p> },
          {
            Component: RequireRole,
            handle: { roles: [Roles.Administrator] },
            children: [{ path: "/admin", element: <p>admin page</p> }],
          },
        ],
      },
      { Component: GuestOnly, children: [{ path: "/guest", element: <p>guest page</p> }] },
    ],
    { initialEntries: [path] },
  );

  const value: AuthContextValue = {
    ...auth,
    login: async () => ({ ok: true }),
    register: async () => ({ ok: true }),
    logout: async () => undefined,
  };

  render(
    <MantineProvider>
      <AuthContext.Provider value={value}>
        <RouterProvider router={router} />
      </AuthContext.Provider>
    </MantineProvider>,
  );
  return router;
}

const anonymous = { status: "anonymous", user: null } as const;
const member = { status: "authenticated", user: testUser() } as const;
const admin = {
  status: "authenticated",
  user: testUser({ userType: "employee", role: Roles.Administrator }),
} as const;

describe("RequireAuth", () => {
  it("sends anonymous visitors to /login and remembers where they wanted to go", () => {
    const router = renderAt("/account?tab=1", anonymous);

    expect(screen.getByText("login page")).toBeInTheDocument();
    expect(router.state.location.search).toBe(`?from=${encodeURIComponent("/account?tab=1")}`);
  });

  it("shows a loader (no redirect) while the silent login is still running", () => {
    renderAt("/account", { status: "loading", user: null });

    expect(screen.getByLabelText("Loading…")).toBeInTheDocument();
    expect(screen.queryByText("login page")).not.toBeInTheDocument();
  });

  it("lets logged-in users through", () => {
    renderAt("/account", member);

    expect(screen.getByText("account page")).toBeInTheDocument();
  });
});

describe("RequireRole", () => {
  it("sends a member away from an administrator page", () => {
    renderAt("/admin", member);

    expect(screen.getByText("forbidden page")).toBeInTheDocument();
  });

  it("lets an administrator through", () => {
    renderAt("/admin", admin);

    expect(screen.getByText("admin page")).toBeInTheDocument();
  });
});

describe("GuestOnly", () => {
  it("shows the page to visitors", () => {
    renderAt("/guest", anonymous);

    expect(screen.getByText("guest page")).toBeInTheDocument();
  });

  it("sends a logged-in user on to ?from", () => {
    renderAt(`/guest?from=${encodeURIComponent("/account")}`, member);

    expect(screen.getByText("account page")).toBeInTheDocument();
  });

  it("ignores a ?from that points to another site", () => {
    renderAt(`/guest?from=${encodeURIComponent("//evil.example")}`, member);

    expect(screen.getByText("home page")).toBeInTheDocument();
  });
});
