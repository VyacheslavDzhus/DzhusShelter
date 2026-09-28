# Public Access & Auth

**Route:** n/a (cross-cutting, not a page) · **Status:** Spec'd · **Architecture:** [docs/architecture.md](../architecture.md)

## Overview

DzhusShelter's `Api` and `UI` become reachable from the public internet (via the existing Cloudflare Tunnel already used for Home Assistant on the CasaOS host), so the dashboard can be checked from anywhere and iOS Shortcuts can push data into it (starting with Screen Time-style stats — the actual ingestion endpoint is a separate, later feature; this spec only opens the door). Exposing `Api` publicly requires auth first — this is the exact precondition [architecture.md](../architecture.md)'s Security section already flagged ("if a public-facing use case ever requires exposing the API directly, auth must be added first").

Two different client shapes need two different auth mechanisms:
- **UI** (Blazor Server, a human sitting at a browser) → JWT issued via a login page, this project's first real exercise of roadmap topic 12 ("Auth (JWT, OAuth, Keycloak)").
- **Bot + iOS Shortcuts** (machine clients, no human login flow, no UI to render a form in) → a static per-client API key.

Both schemes protect the same `Api`; most endpoints accept either.

## Network exposure

- No new `cloudflared` container. The existing CasaOS app (`wisdomsky/cloudflared-web`, **Network: host**) already shares the host's network namespace, so it can already reach `localhost:8082` (`ui`) and, after the compose change below, `localhost:<api-port>` too.
- Two new Public Hostname entries added to that same tunnel (via Cloudflare Zero Trust dashboard, or this image's own Web UI on `:14333` if it exposes the same setting): `dashboard.<domain>` → `ui`, `api.<domain>` → `api`.
- `docker-compose.yml` change: the `api` service gains a `ports:` mapping bound to `127.0.0.1` only (e.g. `"127.0.0.1:5279:8080"`) — reachable from processes on the CasaOS host itself (including the host-networked tunnel), never from the LAN or internet directly without going through the tunnel + auth.
- `ui`'s existing mapping (`"8082:8080"`) also gets narrowed to `"127.0.0.1:8082:8080"`. Today, with no bind address, Docker publishes it on `0.0.0.0` — reachable from anywhere on the LAN, not just the host. That was an acceptable gap while `ui` was LAN-only; once it's also reachable from the internet via the tunnel, the same rule as `api` should apply: the only way in is through Cloudflare, never by hitting `<host-ip>:8082` directly from another device on the network.
- Since Cloudflare Tunnel traffic always proxies through Cloudflare's edge, its Bot Fight Mode / managed WAF rules (whatever the account's plan includes) apply for free on both new hostnames — worth turning on in the Cloudflare dashboard as a second layer, not a replacement for the app-level auth below.

## Auth architecture (`DzhusShelter.Api`)

Two ASP.NET Core authentication schemes registered side by side in `Program.cs`:

- **`Bearer` (JWT)** — `AddJwtBearer(...)`, symmetric `HMACSHA256` key from `JWT_SIGNING_KEY`. Validates tokens issued by this same Api's own login endpoint.
- **`ApiKey`** — a custom `AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>` that reads the `X-Api-Key` header and compares it against the configured per-client keys (`API_KEY_BOT`, `API_KEY_SHORTCUTS`). On success, builds a `ClaimsPrincipal` with a `client_name` claim (`"bot"` or `"shortcuts"`) — not for permissions (single-user data, nothing is restricted per-client), just so logs/handlers can tell who called.

A combined authorization policy, e.g. `"ApiClient"`:

```csharp
options.AddPolicy("ApiClient", policy =>
    policy.AddAuthenticationSchemes("Bearer", "ApiKey")
          .RequireAuthenticatedUser());
```

Applied via `[Authorize(Policy = "ApiClient")]` on `BadHabitsController` (and every controller added after it) — one endpoint, either credential type satisfies it. No per-endpoint scheme branching.

## Login endpoint

- `POST /api/auth/login` — body `{ "password": "..." }` (no username; single admin, matches this project's existing "single user, no user management" posture — a `Users` table would be over-engineering here).
- New `Application/Auth/Commands/LoginCommand.cs` (+ handler) — same CQRS shape as `LogHabitEntryCommand`: `LoginCommand(string Password)` → `LoginCommandHandler` → `Result<string>` (the JWT, or a generic "invalid credentials" error — never distinguish "wrong password" from "no such thing" in the response, to avoid leaking which is true).
- Password stored as a **bcrypt hash** (`BCrypt.Net-Next` package) in `ADMIN_PASSWORD_HASH` — generated once, offline, never the plaintext in `.env`.
- JWT claims: just enough to identify "the admin" (e.g. a fixed `sub: "admin"`), **no refresh token for v1** — a longer expiry (7 days) is the deliberate simplification; re-login when it lapses. Refresh-token rotation is real complexity this single-device, single-user dashboard doesn't need yet.
- **Rate limiting on this endpoint specifically**: ASP.NET Core's built-in rate limiter (`AddRateLimiter`, a fixed-window policy — e.g. 5 attempts/minute per IP) applied to `/api/auth/login` only. This is now a password check reachable from the entire internet; a few lines of built-in middleware meaningfully raise the cost of brute-forcing it and cost nothing to add now rather than "later."

## UI integration (`DzhusShelter.UI`)

- New `Components/Pages/Login.razor` (`@page "/login"`, no `[Authorize]` — it's the one page reachable without a token). Posts the password to `Api`'s login endpoint through the existing typed `HttpClient`.
- On success, the Blazor Server process sets an **HttpOnly + Secure + SameSite=Strict** cookie holding the JWT (`HttpContext.Response.Cookies.Append`) — not `localStorage`/`sessionStorage`, so client-side script can never read the token even if something else on the page were compromised.
- Every subsequent Blazor→Api call reads the token back out of that cookie server-side and sets it as `Authorization: Bearer <token>` on the outgoing `HttpClient` request.
- A guard at the `MainLayout` level: no valid token in the cookie → redirect to `/login`. No full ASP.NET Core Identity / cookie-auth middleware stack — one password, one cookie, one check.
- Logout: clear the cookie, redirect to `/login`.

## Bot integration (`DzhusShelter.TelegramBot`)

- The existing typed `HttpClient` (already configured with `Api__BaseUrl`) gets a default header `X-Api-Key: {API_KEY_BOT}` set once at startup from configuration — the same pattern already used for `TELEGRAM_BOT_TOKEN`.

## Shortcuts integration

- No code — document in [docs/casaos-runbook.md](../casaos-runbook.md) how to add the header `X-Api-Key: {API_KEY_SHORTCUTS}` to a Shortcut's "Get/Post Contents of URL" action once `api.<domain>` is live.

## Config / secrets

New lines in `.env` (same file, same pattern as the existing three):

```
JWT_SIGNING_KEY=...        # random 32+ byte secret
ADMIN_PASSWORD_HASH=...    # bcrypt hash, generated once, offline
API_KEY_BOT=...
API_KEY_SHORTCUTS=...
```

No `CLOUDFLARE_TUNNEL_TOKEN` needed here — the existing `cloudflared` app on CasaOS owns its own tunnel credential already; this project doesn't touch it.

## Persistence

No new tables. The admin credential and API keys are config (`.env`), not domain data — there is exactly one admin and a small, fixed set of machine clients; a `Users`/`ApiClients` table would be schema for data that never actually varies at runtime.

## Open Decisions (resolved during brainstorming)

- **Scope of exposure**: both `Api` and `UI` go public (not `Api`-only) — the user wants to check the dashboard from outside the LAN too, not just push data into it.
- **Tunnel mechanism**: Cloudflare Tunnel over DNS + router port-forward — no exposed router port, home IP never appears in DNS.
- **No second `cloudflared` container**: the CasaOS host already runs one (host network mode, shared for Home Assistant) — new public hostnames are added to that same tunnel; `api` just needs a `127.0.0.1`-bound host port for it to reach.
- **Auth mechanism split**: JWT for the human-facing `UI` login (this is the project's roadmap-topic-12 JWT practice); a plain per-client API key for the two machine clients (bot, Shortcuts) — full JWT (with a token-issuing flow) would be pure ceremony for a client that can't run a login flow at all.
- **Per-client API keys, not one shared key**: `API_KEY_BOT` and `API_KEY_SHORTCUTS` are separate, so either can be rotated/revoked independently and request logs can tell them apart.
- **No refresh tokens, no `Users` table**: both are real complexity this single-user, single-device project doesn't need yet — noted explicitly as deferred, not overlooked.

## Out of Scope

- The actual "receive stats from other apps" endpoint(s) that Shortcuts will call (Screen Time, etc.) — that's [docs/specs/screen-time.md](screen-time.md)'s concern once its turn comes in the roadmap; this spec only makes it possible to reach `Api` safely from a Shortcut.
- OAuth / Keycloak (later half of roadmap topic 12) — only relevant once there's more than one real user.
- Any per-client permission model — every credential (JWT or API key) currently has equal access to all data, because there is only one user's data to access.
