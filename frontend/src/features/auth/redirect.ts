/**
 * Turns the "?from=..." value of the login URL into a safe place to go after logging in.
 *
 * Only paths inside this app are allowed. Without this check, a link like
 * /login?from=https://evil.example would send the user to another site right after they typed
 * their password there ("open redirect"). "//evil.example" is also a full URL to the browser.
 */
export function safeRedirectTarget(from: string | null | undefined): string {
  if (!from || !from.startsWith("/") || from.startsWith("//") || from.startsWith("/\\")) {
    return "/";
  }
  return from;
}
