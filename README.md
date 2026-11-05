# Piggy3

A small self-hosted web app for tracking spending against virtual accounts.
Three tabs: **Virtuelle Konten** (read-only balances), **Monatliche Kosten**
(editable monthly costs, which double as the autofill list for bookings), and
**Buchungen** (fast keyboard entry, editable in place).

## Run with Docker

```sh
cp .env.example .env
# edit .env: set AUTH_USERNAME, AUTH_PASSWORD and a unique AUTH_JWT_SECRET
docker compose up -d --build
```

Open <http://localhost:5000> and sign in.

## Configuration

All settings come from `.env`:

| Variable | Meaning |
| --- | --- |
| `ACCOUNT_TYPES` | Comma-separated account types, e.g. `Ausgabe,Sparen,Haus` |
| `AUTH_USERNAME` | Login user name |
| `AUTH_PASSWORD` | Login password |
| `AUTH_JWT_SECRET` | HMAC secret for API tokens, at least 32 characters |
| `MONGO_DATABASE_NAME` | Database name (default `piggy3`) |
| `FRONTEND_PORT` | Host port for the frontend (default `5000`) |

Adding a monthly cost creates the virtual account. Deleting a monthly cost
soft-deletes the account: it disappears from the balances and the booking
dropdown, but its bookings stay in the total. Tick "Gelöschte anzeigen" under
Monatliche Kosten to restore it.

Bookings are stored exactly as entered: expenses negative, income positive.
The account is guessed when the entered value (ignoring the sign) exactly
matches a monthly cost. Totals are net.

## Development

Requires .NET 10 and a local MongoDB on `localhost:27017`.

```sh
# terminal 1
AUTH_USERNAME=kai AUTH_PASSWORD=secret \
AUTH_JWT_SECRET=0123456789abcdef0123456789abcdef \
dotnet run --project src/Piggy3.Api --urls http://localhost:5080

# terminal 2
API_BASE_URL=http://localhost:5080 \
dotnet run --project src/Piggy3.Frontend --urls http://localhost:5090
```

## Tests

```sh
dotnet test tests/Piggy3.Api.Tests
```

End-to-end tests use Playwright against a running stack:

```sh
cd tests/e2e
npm install
npx playwright install chromium
BASE_URL=http://localhost:5000 AUTH_USERNAME=admin AUTH_PASSWORD=admin npx playwright test
```

## Authentication

Login is username/password from `.env`. The API issues a JWT; the frontend
stores it inside its encrypted auth cookie and calls the API server-side.
Switching to Nextcloud OIDC later replaces the frontend login step and lets the
API validate OIDC tokens; the data model and pages do not change. OIDC is not
implemented yet.
