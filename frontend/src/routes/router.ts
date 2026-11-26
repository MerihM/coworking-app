// All application routes in one place.
// "Component" (instead of "element: <X />") keeps this file free of JSX, so it can be a .ts file.
import { createBrowserRouter } from "react-router";
import { AppLayout } from "../components/AppLayout";
import { HomePage } from "../pages/HomePage";
import { NotFoundPage } from "../pages/NotFoundPage";

export const router = createBrowserRouter([
  {
    path: "/",
    Component: AppLayout, // shared frame (header, mobile menu); child pages render in its <Outlet />
    children: [
      { index: true, Component: HomePage }, // "/"
      { path: "*", Component: NotFoundPage }, // any unknown URL
    ],
  },
]);
