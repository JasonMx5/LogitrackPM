# LogiTrack API Architecture

## Architecture

- ASP.NET Core Web API controllers use EF Core and SQLite for persistence. ASP.NET Core Identity manages users and roles; JWT bearer tokens authenticate API requests.
- Each authenticated user has a database-backed cart. Checkout validates and decrements inventory in a transaction, then records order-item snapshots.
- Inventory and order reads use short-lived in-memory caching. The main API workflows are illustrated in [test.http](./test.http).

## Key decisions

- Keep the project on .NET Core 2.1 for now to avoid a runtime migration. This framework is unsupported and build output reports known security advisories.
- Require confirmed email for login; send confirmation and password-reset instructions through configured SMTP. Apply account lockout and per-IP auth throttling.
- Access tokens expire after 15 minutes. Secrets such as the JWT signing key and SMTP credentials should come from environment configuration or a secret provider.
- The cache and rate limiter are process-local. A multi-instance deployment needs shared caching and gateway or distributed rate limiting. Refresh tokens and individual token revocation are not implemented; signing-key rotation invalidates all access tokens.

## Submission checklist

- [x] Persistent state implementation verified.
- [x] Full workflow tested with an isolated SQLite database and local TLS SMTP sink: email confirmation, password reset, login, rate limiting, protected API access, cache invalidation, cart persistence across restart, checkout, stock decrement, and order snapshots.
- [x] Code optimized using Copilot recommendations.
- [x] Redundant or unnecessary logic removed.
