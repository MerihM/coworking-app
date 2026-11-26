// All application routes in one place.
// "Component" (instead of "element: <X />") keeps this file free of JSX, so it can be a .ts file.
//
// Routes WITHOUT a path (only Component + children) are "layout routes": guards that wrap a group
// of pages. Their Component renders <Outlet /> (the page) or a redirect.
import { createBrowserRouter } from "react-router";
import { AppLayout } from "../components/AppLayout";
import { GuestOnly } from "../features/auth/GuestOnly";
import { RequireAuth } from "../features/auth/RequireAuth";
import { AccountPage } from "../pages/AccountPage";
import { ForbiddenPage } from "../pages/ForbiddenPage";
import { HomePage } from "../pages/HomePage";
import { LoginPage } from "../pages/LoginPage";
import { NotFoundPage } from "../pages/NotFoundPage";

export const router = createBrowserRouter([
  {
    path: "/",
    Component: AppLayout, // shared frame (header, mobile menu); child pages render in its <Outlet />
    children: [
      { index: true, Component: HomePage }, // "/" – everyone

      // Only for visitors who are NOT logged in.
      { Component: GuestOnly, children: [{ path: "login", Component: LoginPage }] },

      // Only for logged-in users.
      {
        Component: RequireAuth,
        children: [
          { path: "account", Component: AccountPage },
          // CW-17 adds, inside RequireAuth:
          // { Component: RequireRole, handle: { roles: [Roles.Administrator] },
          //   children: [{ path: "admin/staff", Component: StaffListPage }] },
        ],
      },

      { path: "forbidden", Component: ForbiddenPage },
      { path: "*", Component: NotFoundPage }, // any unknown URL
    ],
  },
]);
