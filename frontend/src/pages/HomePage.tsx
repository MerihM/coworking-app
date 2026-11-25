import { ApiStatus } from "../features/health/ApiStatus";

/** Start page. For now it only proves that the frontend can reach the API. */
export function HomePage() {
  return (
    <section>
      <h1>Coworking center</h1>
      <p>Workspace reservations, memberships and payments in one place.</p>
      <ApiStatus />
    </section>
  );
}
