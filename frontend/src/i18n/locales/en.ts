// English texts – the DEFAULT language and the source of truth for the key structure.
// bs.ts must have exactly the same keys (TypeScript checks it), and i18next.d.ts makes t("...")
// accept only keys that exist here.
//
// Group keys by screen/feature ("nav", "home", "health"...). Never build keys from user input.
// {{name}} = a value filled in by the code, e.g. t("validation.tooLong", { max: 100 }).
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
  userMenu: {
    label: "Account menu",
  },
  auth: {
    loginLink: "Log in",
    registerLink: "Register",
    logout: "Log out",
    login: {
      title: "Log in",
      email: "E-mail",
      password: "Password",
      submit: "Log in",
      noAccount: "No account yet?",
      registerLink: "Register",
    },
    register: {
      title: "Create an account",
      firstName: "First name",
      lastName: "Last name",
      email: "E-mail",
      phone: "Phone",
      phoneHint: "e.g. +387 61 123 456",
      password: "Password",
      passwordHint: "At least 8 characters, with a letter and a digit.",
      confirmPassword: "Repeat password",
      submit: "Register",
      haveAccount: "Already have an account?",
      loginLink: "Log in",
      success: "Welcome! Your account has been created.",
    },
  },
  validation: {
    required: "This field is required.",
    tooLong: "At most {{max}} characters.",
    email: "Enter a valid e-mail address.",
    phone: "Only digits, spaces and + ( ) / - are allowed.",
    passwordLength: "At least {{min}} characters.",
    passwordLetter: "The password must contain at least one letter.",
    passwordDigit: "The password must contain at least one digit.",
    passwordsDiffer: "The passwords don't match.",
  },
  errors: {
    invalidCredentials: "Wrong e-mail or password.",
    emailTaken: "An account with this e-mail already exists.",
    tooManyRequests: "Too many attempts. Wait a minute and try again.",
    sessionExpired: "Your session has expired. Please log in again.",
    forbidden: "You are not allowed to do this.",
    duplicate: "This value is already in use.",
    inUse: "This record is still used by other data and can't be deleted. Deactivate it instead.",
    selfChange: "You can't do this with your own account.",
    lastAdmin: "At least one active administrator must remain.",
    network: "The server can't be reached. Check your connection and try again.",
    server: "Something went wrong on the server. Please try again later.",
    unknown: "Something went wrong. Please try again.",
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
