import { Link } from "react-router";

/** Shown for any URL that doesn't match a route. */
export function NotFoundPage() {
  return (
    <section>
      <h1>Page not found</h1>
      <p>
        The page you are looking for doesn't exist. <Link to="/">Back to the start page</Link>
      </p>
    </section>
  );
}
