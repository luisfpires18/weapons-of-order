# Weapons of Order: Authentication & Security V1

This document is the implementation authority for Browser V1 authentication/session/security foundations.

## V1 account model

Browser V1 uses a Weapons of Order account backed by **ASP.NET Core Identity + PostgreSQL**.

Initial login method:
- email + password.

Steam authentication/account linking is deferred until Steam work begins.

Do not make the database identity depend on an external provider. The stable game/account identity is an internal User ID so external login methods can be linked later.

Conceptually:

`WoO UserId -> player/account data`

Future:

`WoO UserId -> linked SteamId`

## Browser session model

Use server-issued **HttpOnly cookies** for the normal authenticated browser session.

Production authentication cookies must use appropriate security attributes, including:
- `HttpOnly`;
- `Secure`;
- an intentional `SameSite` policy compatible with the same-origin architecture;
- explicit expiration/session behavior.

Do not put long-lived authentication tokens into `localStorage` or ordinary JavaScript-readable storage.

The browser client may ask the API who the current user is. The server remains the authority for identity and authorization.

## Registration

Registration requires:
- valid email;
- password accepted by the configured Identity password policy.

Use normalized/unique email semantics through Identity.

Do not create separate custom password hashing code.

Do not expose whether another person's account exists through unnecessarily specific public error messages.

Email confirmation should be supported by the account architecture. During local development a development email/token flow may be used until a production email provider is selected.

The exact production email delivery provider is an infrastructure/configuration choice and is not a game-design blocker.

## Login/logout

Login:
- validates credentials server-side through Identity;
- establishes the secure cookie session;
- applies rate limiting/lockout protections;
- returns generic failure behavior for invalid credentials.

Logout:
- invalidates/signs out the server authentication session;
- returns the user to the unauthenticated shell.

Sensitive state must not remain visible from stale client memory after logout.

## Password recovery

Use Identity's reset-token flow.

Password reset messages must not reveal whether a submitted address belongs to an account.

Do not store reset tokens in plaintext as custom permanent database fields.

## Authorization rule

Authentication answers **who the caller is**.

Authorization must independently answer **whether that caller may perform the action**.

For every player-owned resource:
- resolve the current User ID from the authenticated server context;
- query/validate ownership server-side;
- never authorize an operation merely because the client posted a matching `UserId`, inventory ID, Unit ID, weapon ID, battle ID, etc.

The client must never be allowed to choose another player's User ID for an authoritative operation.

## Server-authoritative game security

Treat the browser as untrusted.

Never accept these as authoritative just because the client submitted them:
- final Unit stats;
- ownership of equipment/resources;
- crafting success/results;
- Rune/weapon validity;
- equipment compatibility;
- currency/resource balances;
- battle RNG;
- combat damage;
- battle winner;
- rewards;
- progression/mastery changes.

The client submits intentions/commands. The server validates and computes results.

## CSRF

Because Browser V1 uses cookie authentication, state-changing requests require CSRF protection.

Use ASP.NET Core antiforgery protection or an equivalent server-validated anti-CSRF mechanism for authenticated mutating requests.

Do not disable antiforgery globally to make React requests easier.

Read-only requests must not mutate authoritative state.

## XSS / content handling

React escaping should remain the default.

Avoid rendering untrusted HTML. Do not use `dangerouslySetInnerHTML` for user-generated data unless there is an explicit reviewed sanitization requirement.

Validate and constrain user-authored text on the server as appropriate to the feature.

## Rate limiting and abuse protection

Apply server-side rate limiting where abuse matters, especially:
- login;
- registration;
- password-reset requests;
- email-confirmation resend;
- future expensive game commands/endpoints where needed.

ASP.NET Core Identity lockout/rate-limit behavior should be configured deliberately rather than relying on unlimited credential attempts.

Exact thresholds are security/configuration values and may be tuned without changing this architecture.

## Validation

Client validation exists for usability only.

All authoritative input is validated again server-side.

Reject malformed, impossible, unauthorized, or stale commands with clear API errors without exposing secrets/internal stack traces.

## Secrets and configuration

Never commit:
- production connection strings;
- signing/secrets;
- email provider credentials;
- Azure credentials;
- future Steam publisher keys;
- other production secrets.

Use local development secrets/environment configuration for development and managed Azure configuration/secrets for deployment.

Frontend build-time environment variables are public to the browser unless proven otherwise. Never treat a Vite client environment variable as secret.

## Database/security hygiene

- Use EF Core parameterization rather than hand-built SQL strings for normal application queries.
- Apply migrations intentionally.
- Use least-privilege production database credentials where practical.
- Do not expose database connections directly to the browser.
- Do not log passwords, auth cookies, reset tokens, connection strings, or equivalent secrets.

## Transport

Production is HTTPS-only.

Secure cookies must never depend on plaintext HTTP in production.

Use the standard Azure/reverse-proxy configuration needed for correct HTTPS/forwarded-header behavior rather than adding custom cryptography.

## PWA security

The PWA service worker must not become an alternate data authority.

Do not cache private authenticated API responses indiscriminately.

On logout/account change, the UI must not intentionally expose cached private account data from another session.

Offline gameplay that can alter authoritative game state is not part of V1.

## Logging and errors

Production errors should be observable through server telemetry without exposing stack traces or secrets to players.

Security-relevant events may be logged with enough context to diagnose abuse while respecting private credential/token data.

## Tests required for auth work

At minimum, auth implementation tasks should verify:
- registration validation;
- successful login;
- failed login;
- logout;
- unauthenticated protected endpoint rejection;
- authenticated own-resource access;
- rejection of cross-account resource access when such resources exist;
- antiforgery behavior for protected mutations;
- session behavior expected by the React client.

Use the `webapp-testing` skill for end-to-end browser verification in addition to focused backend/frontend tests.

## Browser V1 as implemented

This section records the concrete choices made where the architecture above left a
configuration decision open. It is a description of the current implementation, not a new
constraint: thresholds and names remain tunable.

Session cookie:
- name `woo.session`, `HttpOnly`, `Path=/`, essential;
- `SameSite=Lax` — client and API share one origin so no legitimate request is cross-site,
  while a plain link into the game still arrives signed in;
- `Secure` always outside Development, `SameAsRequest` in Development so local plain-http
  works;
- 14-day sliding expiration;
- unauthenticated API calls answer `401` with ProblemDetails rather than redirecting to an
  HTML login page.

Antiforgery:
- ASP.NET Core antiforgery, cookie `woo.antiforgery` (`HttpOnly`), request token echoed in
  the `X-WoO-Antiforgery` header;
- the request token is published by `GET /api/auth/session`, which a cross-site page cannot
  read;
- validated by an endpoint filter applied to the whole mutating route group, including the
  unauthenticated flows, because login CSRF is a real attack and a uniform rule is easier to
  keep correct than an exemption list.

Email confirmation:
- required before a session can be established, configurable through
  `Auth:RequireConfirmedEmailForSignIn`;
- enforced by the login endpoint **after** the password check, not through
  `IdentityOptions.SignIn.RequireConfirmedAccount`. Identity's own check runs before the
  password is verified, which would let anyone discover which addresses are registered.

Account link origin:
- built only from `Auth:ClientBaseUrl`, which must be an absolute https origin, or http for
  a loopback host during local development, carrying no query or fragment;
- **never** derived from the request. The `Host` header is attacker-controlled, so a link
  built from it could point at the attacker's domain and still be mailed to the account
  holder;
- validated at startup, so a deployment without one refuses to boot rather than discovering
  it on somebody's first password reset — a failure that appeared only for addresses that
  exist would itself be an account-existence oracle.

Development email delivery:
- no production provider is configured yet;
- in the Development environment only, confirmation and reset links are captured in a
  bounded in-memory outbox and published by `GET /api/dev/account-notifications`;
- links and tokens are **never** written to a log, in any environment, including
  Development: logs are copied, shipped and retained in places the in-memory outbox is not;
- every other environment uses a sender that records only that a message was dropped;
- the public responses are identical either way, so the capture does not change what the API
  discloses.

Current thresholds (all configuration under `Auth`):
- password: 12 characters minimum, 4 unique, no character-class requirements;
- lockout: 5 failed attempts, 15 minutes;
- rate limits per caller address: login 10 per 5 minutes, registration 5 per 15 minutes,
  password reset and confirmation resend 5 per 15 minutes.

Deployment still has to set:
- forwarded-header handling, so rate-limit partitions see the real caller behind a proxy;
- a real email delivery provider. Until one exists, no confirmation or reset message leaves
  a non-Development environment.

## Explicitly deferred

- Steam login/linking;
- Google/Apple/social login;
- 2FA requirement for ordinary players;
- admin/staff authorization model beyond what an actual admin feature requires;
- native-app token flows;
- OAuth authorization server behavior;
- anonymous/guest progression unless explicitly requested later.

## Security change rule

Do not weaken a security control merely to simplify a frontend implementation.

When a security mechanism creates friction, solve the integration correctly or surface the tradeoff for review.
