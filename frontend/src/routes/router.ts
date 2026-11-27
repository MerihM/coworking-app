// All application routes in one place.
// "Component" (instead of "element: <X />") keeps this file free of JSX, so it can be a .ts file.
//
// Routes WITHOUT a path (only Component + children) are "layout routes": guards that wrap a group
// of pages. Their Component renders <Outlet /> (the page) or a redirect.
import { createBrowserRouter } from "react-router";
import { AppLayout } from "../components/AppLayout";
import { GuestOnly } from "../features/auth/GuestOnly";
import { RequireAuth } from "../features/auth/RequireAuth";
import { RequireRole, type RouteHandle } from "../features/auth/RequireRole";
import { Roles } from "../features/auth/roles";
import { AccountPage } from "../pages/AccountPage";
import { AvailabilityPage } from "../pages/AvailabilityPage";
import { ForbiddenPage } from "../pages/ForbiddenPage";
import { HomePage } from "../pages/HomePage";
import { LoginPage } from "../pages/LoginPage";
import { NotFoundPage } from "../pages/NotFoundPage";
import { RegisterPage } from "../pages/RegisterPage";
import { StaffListPage } from "../pages/admin/StaffListPage";
import { MembershipTypesPage } from "../pages/manage/MembershipTypesPage";
import { ReportPage } from "../pages/manage/ReportPage";
import { MyMembershipPage } from "../pages/member/MyMembershipPage";
import { MyReservationsPage } from "../pages/member/MyReservationsPage";
import { MemberDetailPage } from "../pages/reception/MemberDetailPage";
import { MembersListPage } from "../pages/reception/MembersListPage";
import { PaymentsPage } from "../pages/reception/PaymentsPage";
import { ReservationsPage } from "../pages/reception/ReservationsPage";
import { RoomDetailPage } from "../pages/manage/RoomDetailPage";
import { RoomsListPage } from "../pages/manage/RoomsListPage";

export const router = createBrowserRouter([
  {
    path: "/",
    Component: AppLayout, // shared frame (header, mobile menu); child pages render in its <Outlet />
    children: [
      { index: true, Component: HomePage }, // "/" – everyone (offer, CW-25)
      { path: "availability", Component: AvailabilityPage }, // FR02 – everyone, no login (CW-25)

      // Only for visitors who are NOT logged in.
      {
        Component: GuestOnly,
        children: [
          { path: "login", Component: LoginPage },
          { path: "register", Component: RegisterPage }, // FR01
        ],
      },

      // Only for logged-in users.
      {
        Component: RequireAuth,
        children: [
          { path: "account", Component: AccountPage },

          // Administrator only (FR12). Other roles are sent to /forbidden.
          {
            Component: RequireRole,
            handle: { roles: [Roles.Administrator] } satisfies RouteHandle,
            children: [{ path: "admin/staff", Component: StaffListPage }], // CW-17
          },

          // Member only (FR03): my membership – buy, cancel an unpaid order, history (CW-27).
          // FR05/FR06: my reservations – upcoming, past, cancel (CW-31).
          {
            Component: RequireRole,
            handle: { roles: [Roles.Member] } satisfies RouteHandle,
            children: [
              { path: "memberships", Component: MyMembershipPage },
              { path: "reservations", Component: MyReservationsPage },
            ],
          },

          // Reception: Receptionist or Manager (FR07, FR08 – CW-28). The administrator gets /forbidden.
          {
            Component: RequireRole,
            handle: { roles: [Roles.Receptionist, Roles.Manager] } satisfies RouteHandle,
            children: [
              { path: "reception/members", Component: MembersListPage },
              { path: "reception/members/:memberId", Component: MemberDetailPage },
              { path: "reception/payments", Component: PaymentsPage },
              { path: "reception/reservations", Component: ReservationsPage }, // CW-31
            ],
          },

          // Manager only (FR09, FR10). Roles don't inherit: the administrator gets /forbidden too.
          {
            Component: RequireRole,
            handle: { roles: [Roles.Manager] } satisfies RouteHandle,
            children: [
              { path: "manage/rooms", Component: RoomsListPage }, // CW-23
              { path: "manage/rooms/:roomId", Component: RoomDetailPage }, // CW-23
              { path: "manage/membership-types", Component: MembershipTypesPage }, // CW-24
              { path: "manage/reports", Component: ReportPage }, // CW-33, FR11 (TC08: Manager only)
            ],
          },
        ],
      },

      { path: "forbidden", Component: ForbiddenPage },
      { path: "*", Component: NotFoundPage }, // any unknown URL
    ],
  },
]);
