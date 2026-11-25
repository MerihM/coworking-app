import { Link, Outlet } from "react-router";

/** Shared page frame: header with the app name, and the current page below it. */
export function Layout() {
  return (
    <div className="layout">
      <header className="layout__header">
        <Link to="/" className="layout__brand">
          Coworking
        </Link>
      </header>

      <main className="layout__main">
        {/* The matched child route (HomePage, NotFoundPage...) is rendered here. */}
        <Outlet />
      </main>
    </div>
  );
}
