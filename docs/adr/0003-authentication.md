# ADR 0003 – Authentication and authorization

- **Status:** Accepted
- **Date:** 2026-11-25
- **Sprint:** 2 (CW-9 – CW-13)
- **Requirements:** NFR01 (login required, hashed passwords, HTTPS, role-based access), FR01, FR12, TC08

## Context

The system has five actors: Visitor (anonymous), Member, Receptionist, Manager and Administrator.
Members and employees are separate classes in the class diagram (`Clan`, `Zaposlenik`) and therefore
separate tables. The frontend is a React single-page app served from the same origin as the API
(Vite dev proxy now, Nginx in production).

We need to: store passwords safely, let both kinds of users log in, keep them logged in across page
reloads, and restrict every function to the roles that own it.

## Decision

### Passwords
`PasswordHasher<T>` from ASP.NET Core Identity (PBKDF2-HMAC-SHA512, 100 000 iterations, random salt,
versioned format), hidden behind our own `IPasswordHasher`. We do **not** adopt full ASP.NET Core
Identity: its `AspNetUsers`/`AspNetRoles` tables would duplicate `members` and `employees`.

### Tokens
| | Access token | Refresh token |
|---|---|---|
| Format | JWT, HMAC-SHA256 | 256-bit random value (opaque) |
| Lifetime | 15 minutes | 7 days, rotated on every use |
| Where the browser keeps it | JavaScript memory only | `cw_refresh` cookie: HttpOnly, Secure, SameSite=Strict, Path=/api/auth |
| Sent as | `Authorization: Bearer …` | Cookie, only to `/api/auth/*` |
| Stored on server | Not stored | SHA-256 hash in `refresh_tokens` |

- **Rotation + reuse detection (CW-10):** every refresh replaces the token. If a token that was already
  replaced is presented again, someone has a copy → the whole token family is revoked.
- **Claims:** `sub`, `user_type` (`member`/`employee`), `role`, `email`, `name`, `jti`. IDs are only
  unique per table, so `sub` is always read together with `user_type`.
- **Validation:** issuer, audience, lifetime (30 s clock skew), signature, and only `HS256` is
  accepted (blocks `alg: none` and algorithm confusion). `MapInboundClaims = false` keeps claim names.
- **Signing key:** at least 256 bits, from user-secrets (dev) or an environment variable (Docker).
  Options are validated at startup; the app refuses to start without a valid key.

### Authorization
- Secure by default: `MapControllers().RequireAuthorization()` – every controller action needs a valid
  token unless marked `[AllowAnonymous]`. `/health`, `/openapi`, `/scalar` are mapped outside
  controllers and stay public.
- Named policies (`AdministratorOnly`, `ManagerOnly`, `ReceptionistOrManager`, `StaffOnly`,
  `MemberOnly`) check both `user_type` and `role`. Roles do not inherit from each other: an
  Administrator is not a Manager (NFR01 – "only functions of their own role").
- Ownership rules ("only your own reservation") are checked in services via `ICurrentUser`.
- 401 and 403 are returned as ProblemDetails with a stable `code` (`auth.unauthenticated`,
  `auth.forbidden`) that the frontend translates.

## Alternatives considered

- **Token in localStorage:** simplest, but any XSS bug (or a compromised npm package) could read
  and steal it. Rejected.
- **Only a long-lived JWT in an httpOnly cookie:** safe from XSS, but a JWT can't be revoked before it
  expires, so logout and "deactivate employee" would not take effect. Rejected.
- **Server-side cookie sessions (ASP.NET cookie auth):** a valid option for a same-origin app; JWT +
  refresh was chosen because it is the more common pattern in API-first projects and is useful to
  learn and to show on a CV.
- **Full ASP.NET Core Identity:** see Passwords.

## CSRF

Cookies are sent automatically by the browser, which is what CSRF attacks abuse. Here the only
cookie is the refresh token, it is `SameSite=Strict` (never sent on requests started from another
site) and limited to `/api/auth`. The refresh endpoint only returns a new access token in the response
body, which another site cannot read (same-origin policy, no CORS). All other endpoints require the
`Authorization` header, which browsers never add automatically. No anti-CSRF token is needed.

## Consequences

- A deactivated user keeps a working access token for at most 15 minutes; their refresh tokens are
  revoked immediately, so the session ends at the next refresh.
- Several open tabs share one refresh cookie, so the frontend serializes refreshes with the Web Locks
  API (`navigator.locks`); otherwise two tabs refreshing at once would trigger reuse detection.
- Every `WebApplicationFactory` in tests must supply Jwt settings (`TestAuthSettings`).
- E-mail uniqueness across both user tables is enforced in the application layer, not by a DB constraint.
