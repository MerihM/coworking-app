// English texts – the DEFAULT language and the source of truth for the key structure.
// bs.ts must have exactly the same keys (TypeScript checks it), and i18next.d.ts makes t("...")
// accept only keys that exist here.
//
// Group keys by screen/feature ("nav", "home", "health"...). Never build keys from user input.
export const en = {
  app: {
    name: "Coworking",
    tagline: "Workspace reservations, memberships and payments in one place.",
  },
  nav: {
    home: "Home",
    openMenu: "Open navigation",
    closeMenu: "Close navigation",
  },
  language: {
    label: "Language",
  },
  home: {
    title: "Coworking center",
  },
  health: {
    label: "API",
    checking: "checking…",
    healthy: "Healthy",
    degraded: "Degraded",
    unhealthy: "Unhealthy (database not reachable)",
    unreachable: "unreachable (is the API running?)",
  },
  notFound: {
    title: "Page not found",
    text: "The page you are looking for doesn't exist.",
    back: "Back to the start page",
  },
};
