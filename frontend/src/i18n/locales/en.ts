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
  common: {
    loading: "Loading…",
  },
  nav: {
    home: "Home",
    account: "My account",
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
  auth: {
    loginLink: "Log in",
    logout: "Log out",
    login: {
      title: "Log in",
      email: "E-mail",
      password: "Password",
      submit: "Log in",
      failed: "Wrong e-mail or password.",
      tooMany: "Too many attempts. Wait a minute and try again.",
    },
  },
  account: {
    title: "My account",
    name: "Name",
    email: "E-mail",
    role: "Role",
    loadFailed: "Your account could not be loaded.",
  },
  roles: {
    member: "Member",
    receptionist: "Receptionist",
    manager: "Manager",
    administrator: "Administrator",
  },
  forbidden: {
    title: "Access denied",
    text: "Your role doesn't allow you to open this page.",
    back: "Back to the start page",
  },
  notFound: {
    title: "Page not found",
    text: "The page you are looking for doesn't exist.",
    back: "Back to the start page",
  },
};
