# Piggy3 — Design

Date: 2026-11-05
Status: awaiting approval

## Intent

A small, self-hosted web app for one person to track spending against virtual
accounts ("virtuelle Konten"). It replaces the more complex `piggy` app with a
single three-tab screen: see account balances, maintain monthly costs, and
enter bookings quickly while checking the total against the real bank account.

## Goals

- Three tabs on one page: **Virtuelle Konten**, **Monatliche Kosten**, **Buchungen**.
- Accounts grouped by a configurable list of types (default `Ausgabe`, `Sparen`, `Haus`).
- Bookings entered quickly from the keyboard, with exact-value account guessing.
- Soft-deleted accounts can be restored.
- Username/password login now, prepared for a later switch to Nextcloud OIDC.
- Optional TOTP two-factor authentication (authenticator app) with a setup page.
- API and frontend as separate projects; Docker Compose with MongoDB.
- Unit tests for the API, end-to-end tests for the frontend.

## Non-goals

- Multiple users or per-user data. There is exactly one owner.
- OIDC login in this iteration. Only the seam is prepared.
- Bank import, CSV, splits, groups, charts, pagination, recurring schedules.
- Editing account history / audit log.

## Decisions (from the conversation)

- An account is a virtual account. Monthly costs are a separate lookup used
  mainly to prefill the account when entering a booking.
- Adding a monthly-cost entry creates the account. Editing the entry renames /
  retypes the account and changes its monthly value.
- Deleting a monthly-cost entry soft-deletes the account. Bookings stay. Deleted
  accounts and their entries are hidden by default and can be shown and restored.
- Bookings are editable and deletable in place (no separate detail page).
- Booking amounts are stored exactly as entered: expenses negative, income
  positive. Totals are net cash flow.
- Account guessing ignores the sign: the absolute entered value is compared to
  the absolute monthly cost value.
- One person only, one login.

## Architecture

```
piggy3/
  src/Piggy3.Api/          ASP.NET Core Web API, MongoDB, JWT auth
  src/Piggy3.Frontend/     ASP.NET Core Razor Pages + Bootstrap, cookie auth (BFF)
  tests/Piggy3.Api.Tests/  xUnit unit tests
  tests/e2e/               Playwright end-to-end tests (TypeScript)
  docker-compose.yml
  .env.example
```

- **API**: JSON over HTTP, stateless, JWT bearer on every endpoint except
  `/api/auth/login`. MongoDB via `MongoDB.Driver`.
- **Frontend**: server-rendered Razor Pages in C#. Page handlers call the API
  server-side through a small `ApiClient` that attaches the JWT. The browser
  only talks to the frontend, so no CORS and no token in browser storage.
- **Frontend auth**: login form posts to its own page, which calls
  `POST /api/auth/login`, then signs the browser in with an encrypted cookie
  that carries the API token as a claim. Logout clears the cookie.
- **OIDC seam**: the frontend authentication is isolated in one place. Later,
  an OpenID Connect handler can replace the login step and the API can validate
  OIDC bearer tokens instead of its own JWT; data and pages do not change.

## Configuration (`.env`)

| Variable | Purpose |
| --- | --- |
| `ACCOUNT_TYPES` | Comma-separated types, e.g. `Ausgabe,Sparen,Haus` |
| `AUTH_USERNAME` | Login user name |
| `AUTH_PASSWORD` | Login password (plain env value, compared in constant time) |
| `AUTH_JWT_SECRET` | HMAC secret for API tokens, at least 32 chars |
| `MONGO_CONNECTION_STRING` | Default `mongodb://mongo:27017` |
| `MONGO_DATABASE_NAME` | Default `piggy3` |
| `API_BASE_URL` (frontend) | Default `http://api:8080` |
| `FRONTEND_PORT` | Host port, default `5000` |

Types are read at startup. If an account already has a type no longer listed,
it keeps that type and still groups under it at the end, and its monthly value
counts toward the grand total. The type dropdown offers configured types plus
the account's current type, so such a row can be saved unchanged. For new
accounts and type changes, only configured types are accepted.

## Data model (MongoDB)

Collection `accounts`:
- `_id` ObjectId
- `name` string
- `type` string
- `isDeleted` bool, default false
- `createdAt`, `updatedAt` DateTime UTC

Collection `monthlyCosts` (one per account):
- `_id` ObjectId
- `accountId` string (ObjectId of account)
- `value` decimal
- `createdAt`, `updatedAt` DateTime UTC

Collection `transactions`:
- `_id` ObjectId
- `date` DateTime UTC at midnight (date only)
- `amount` decimal (negative allowed)
- `accountId` string (must reference an existing account, including deleted ones)
- `createdAt`, `updatedAt` DateTime UTC

Collection `totpSecrets` (one per user, only when 2FA is enabled):
- `_id` ObjectId
- `userId` string (configured user name)
- `secret` string (Base32 TOTP secret)
- `createdAt` DateTime UTC

No seeded data. The first login shows empty tables.

## API

All endpoints require authentication except `POST /api/auth/login`.
Invalid input returns `400` with a Problem Details body; missing account or
transaction returns `404`.

### Auth

- `POST /api/auth/login` — body `{ "username": "...", "password": "..." }`.
  On success returns `{ token, challengeToken, twoFactorRequired, username }`.
  Without 2FA `token` is set; with 2FA `twoFactorRequired` is true,
  `challengeToken` is a 5-minute token with a separate audience, and `token`
  is null. `401` for wrong credentials.
- `POST /api/auth/2fa/verify` — body `{ challengeToken, code }`, anonymous.
  Validates the challenge and TOTP code, returns a normal login response with
  an access token, or `401`.
- `GET /api/auth/2fa` — `{ enabled }` for the current user.
- `POST /api/auth/2fa/setup` — returns `{ secret, otpauthUri }` for a new
  secret; nothing is stored yet.
- `POST /api/auth/2fa/enable` — body `{ secret, code }`; verifies the code,
  stores the secret, returns `204`. Invalid code returns `400`.
- `DELETE /api/auth/2fa` — removes the stored secret, returns `204`.
- `GET /api/account-types` — returns configured type list, in configured order.

Challenge tokens use audience `piggy3-2fa` and are rejected by the normal API
bearer validation, which requires audience `piggy3`.

### Accounts (tab 1)

- `GET /api/accounts` — active accounts with `{ id, name, type, sum }`, where
  `sum` is the net sum of that account's transaction amounts. Sorted by type
  order, then name. Frontend groups the rows.

### Monthly costs (tab 2)

- `GET /api/monthly-costs?includeDeleted=false` — rows
  `{ accountId, name, type, value, isDeleted }`, sorted by type order, then name.
- `POST /api/monthly-costs` — body `{ name, type, value }`. Creates the account
  (active) and its monthly cost. Name must be unique among active accounts.
- `PUT /api/monthly-costs/{accountId}` — body `{ name, type, value }`. Updates
  account name/type and monthly value. Renaming keeps the same id, so bookings
  follow it.
- `DELETE /api/monthly-costs/{accountId}` — soft-deletes the account.
- `POST /api/monthly-costs/{accountId}/restore` — clears `isDeleted`.

### Transactions (tab 3)

- `GET /api/transactions` — `{ items: [{ id, date, amount, accountId, accountName }], total }`,
  newest date first, then newest created first. `total` is the net sum of all
  transaction amounts, regardless of account deletion.
- `POST /api/transactions` — body `{ date, amount, accountId }`.
- `PUT /api/transactions/{id}` — body `{ date, amount, accountId }`.
- `DELETE /api/transactions/{id}`.

`GET /api/transactions` includes bookings of soft-deleted accounts (with the
account name) because the total must match the bank account.

## Frontend behaviour

Single Razor page `/` with Bootstrap 5 tabs. The active tab survives reloads
via `?tab=konten|kosten|buchungen`, so form submissions return to the right tab.
`/Login` is the only other page; all pages require login.

When 2FA is enabled, `/Login` stores the short-lived challenge and user name in
HttpOnly cookies and redirects to `/TwoFactor`, where the code is verified.
`/TwoFactor/Setup` (linked as "2FA" in the navbar) shows a QR code and the
manual secret, verifies one code, and then shows the enabled state with a
disable button. The TOTP secret is stored only after a valid code is entered.

### Tab 1 — Virtuelle Konten

Read-only. For each configured type in order, a heading and a table
`Account | Type | Sum`. Accounts whose type is no longer configured appear in
their own group at the end. Amounts formatted German (`1.234,56 €`) and
negative values shown in red. Empty state: "Noch keine Konten."

### Tab 2 — Monatliche Kosten

At the top, one total per configured type, then a grand total — computed
server-side from the monthly values of active accounts and rendered as the
prompt's summary (e.g. `Ausgabe | 123,00 €` … `Total | 811,00 €`).

Below, a table `Account | Value | Type` with one row per active account;
each row has an in-place edit form (name text, value text accepting comma or
dot, type dropdown), a save button, and a delete button. A compact "add" row
at the bottom creates a new entry/account. A checkbox "Gelöschte anzeigen"
(`?showDeleted=true`) reveals soft-deleted rows with a restore button.

### Tab 3 — Buchungen

Entry row `[Date] [Value] [Account]` as a Bootstrap form:
- Date input is `type=date`, defaults to today on page load, and can be typed
  manually (native picker available).
- Value is a text input accepting German comma or dot notation, including
  negatives.
- Account is a `<select>` of all active account names.
- On value entry (input/change), JavaScript parses the value and compares its
  absolute value (rounded to cents) exactly with the absolute active monthly
  cost values. On exactly one match it selects that account. Ambiguous matches
  leave the selection unchanged. No fuzzy or near matches.
- When the Buchungen tab becomes active (including after reload with
  `?tab=buchungen`), focus goes to the value field. Explicit tab order is
  value → account → date; Enter in any field submits the form.
- Under the form, the net total of all transactions, then the table
  `Date | Account | Value`. Each row has an edit action that swaps the row for
  date/value/account inputs with save/cancel, and a delete action. Enter saves,
  Escape cancels. All actions post to frontend handlers, so the table and total
  are current after reload.
- A second row offers a type dropdown and an "alle Buchen" button. It creates
  one booking dated today for every active monthly cost of the selected type,
  using the stored value unchanged (zero values are skipped), so recurring costs
  can be entered in one step.

Money parsing: trim, remove spaces and `€`, convert a single comma to a dot,
then parse with invariant culture; reject empty or multiple separators.
Parsed values are rounded to two decimals before comparison and submission.
Formatting uses German `de-DE` with two decimals and the `€` suffix.

## Validation (API)

- Name: required, trimmed, unique among active accounts (case-sensitive).
- Type: required; configured types, or the account's current type on update.
- Monthly value / booking amount: valid decimal; both signs allowed. Values are
  rounded half away from zero to two decimals on write, so every stored amount
  is in cents.
- Date: required, valid date; stored as date at UTC midnight.
- Booking account: must exist (active or soft-deleted).
- Restore: rejected with `400` if an active account already uses that name;
  the account stays deleted. The UI shows a message.

## Docker

`docker-compose.yml` starts three services:
- `mongo` — `mongo:7`, named volume, no host port required.
- `api` — built from `src/Piggy3.Api/Dockerfile`, internal port 8080.
- `frontend` — built from `src/Piggy3.Frontend/Dockerfile`, host port from
  `.env` (default 5000), `API_BASE_URL=http://api:8080`.

`.env.example` documents every variable; `.env` is git-ignored. `docker compose
up --build` is the whole setup. The API waits for Mongo by retrying the first
operation; no migration step is needed.

## Testing

**API unit tests** (`dotnet test tests/Piggy3.Api.Tests`): money parse/format,
login (correct/incorrect credentials, missing config), TOTP setup/verify/enable/
disable, challenge-token isolation from API bearer tokens, account totals,
monthly cost create/update/soft-delete/restore rules, transaction CRUD
validation, type filtering, and HTTP status codes via `WebApplicationFactory`
with mocked repositories or an in-memory Mongo substitute. Controllers/helpers
carry the logic; tests target them directly. No test requires a running MongoDB.

**Frontend e2e tests** (`tests/e2e`, Playwright): start the Compose stack, log
in, then:
- tab 1 shows seeded accounts grouped by type with correct sums;
- tab 2 shows type totals and grand total, add/edit/delete/restore works;
- tab 3 opens with focus in the value field, typing `53,80` selects the matching
  account, Tab order works, Enter adds the booking and updates the total,
  negative booking is accepted, in-place edit and delete work.

Tests create uniquely named accounts so they can run against a fresh or
existing database.

## Verification

- `dotnet build` succeeds for API and frontend.
- `dotnet test tests/Piggy3.Api.Tests` passes.
- `docker compose up --build -d` serves the app on `http://localhost:5000`.
- `npx playwright test` passes against the running stack.

## Builder calls (not user decisions)

- Razor Pages BFF instead of Blazor/SPA: least code while keeping the API
  separate, no browser token, and native Bootstrap forms.
- One monthly cost per account, enforced by the create/update endpoints.
- An account type that disappears from config keeps its stored type, gets its
  own total group at the end of tab 2, and stays selectable on its own row.
- Restore into a name that an active account now uses is rejected, never
  auto-renamed and never allowed as a duplicate.
- All money is stored in cents; values are rounded to two decimals on write.
- Soft-deleted accounts remain valid targets when editing old bookings, but do
  not appear in the entry dropdown or guessing list.
- Totals are net (income minus expenses); no separate "spent" figure is shown.
