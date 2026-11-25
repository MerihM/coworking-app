// All application routes in one place.
// "Component" (instead of "element: <X />") keeps this file free of JSX, so it can be a .ts file.
import { createBrowserRouter } from "react-router";
import { Layout } from "../components/Layout";
import { HomePage } from "../pages/HomePage";
import { NotFoundPage } from "../pages/NotFoundPage";

export const router = createBrowserRouter([
  {
    path: "/",
    Component: Layout, // shared frame; child pages render in its <Outlet />
    children: [
      { index: true, Component: HomePage }, // "/"
      { path: "*", Component: NotFoundPage }, // any unknown URL
    ],
  },
]);
