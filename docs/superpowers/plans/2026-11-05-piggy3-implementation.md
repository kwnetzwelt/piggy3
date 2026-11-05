# Piggy3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a three-tab self-hosted money app with a separate ASP.NET Core API, Razor Pages frontend, MongoDB, and username/password login.

**Architecture:** A `Piggy3.Api` Web API owns MongoDB and JWT auth. `Piggy3.Frontend` is a Razor Pages BFF that signs the browser in with a cookie, calls the API server-side, and renders Bootstrap tabs. `Piggy3.Shared` holds the JSON contracts and the single money parser used by both sides.

**Tech Stack:** .NET 10 / C#, ASP.NET Core Web API, Razor Pages, MongoDB.Driver 2.28.0, Bootstrap 5.3.3 (CDN), xUnit 2.9.3, Playwright 1.64.1, Docker Compose, Mongo 7.

**Spec:** `docs/superpowers/specs/2026-11-05-piggy3-design.md`

## Global Constraints

- Target framework `net10.0`; nullable and implicit usings enabled in every project.
- API and frontend are separate projects; the browser talks only to the frontend.
- All money is stored in cents: parse, then round half away from zero to 2 decimals.
- Amounts are stored exactly as entered (expenses negative, income positive); totals are net.
- Account guessing matches the **absolute** value exactly against absolute monthly cost values, rounded to cents.
- Account types come from `ACCOUNT_TYPES` (default `Ausgabe,Sparen,Haus`), order preserved.
- UI language is German, amounts formatted `de-DE` with `N2` and ` €`.
- All API routes except `POST /api/auth/login` require a JWT.
- `.env` is git-ignored; `.env.example` documents every variable.
- Do not add features outside the spec (no pagination, charts, CSV, multi-user, or OIDC implementation).
- Every commit message uses a conventional prefix (`feat:`, `test:`, `chore:`).

## Review Focus

These are the failure modes most likely to hurt the user; each has a test in its owning task.

1. **Money parsing:** `-53,80`, `53.80`, `1 234,56 €` work; `1.234,56` and junk are rejected; `53,805` becomes `53,81`. A wrong parse silently corrupts totals and guessing. (Task 1, Task 3, Task 8)
2. **Sign-independent guessing:** `+53,80` and `-53,80` both select the `53,80` monthly cost; two equal costs must not guess. (Task 8, Task 10)
3. **Soft-deleted accounts:** hidden from tab 1, the add dropdown and guessing, but their bookings remain in the net total; restore fails if the name was reused. (Task 4, Task 5, Task 10)
4. **Type removed from config:** existing accounts keep their group at the end and count in totals; their row can still be saved. (Task 4, Task 10)
5. **First run / empty data:** no accounts, no costs, no bookings renders empty tables and `0,00 €` totals without errors. (Task 7, Task 10)

---

### Task 1: Solution scaffold, shared contracts, money parser

**Files:**
- Create: `.gitignore`, `.dockerignore`, `Piggy3.sln`
- Create: `src/Piggy3.Shared/Piggy3.Shared.csproj`
- Create: `src/Piggy3.Shared/Money.cs`
- Create: `src/Piggy3.Shared/Contracts.cs`
- Create: `src/Piggy3.Api/Piggy3.Api.csproj`, `src/Piggy3.Api/Program.cs`, `src/Piggy3.Api/appsettings.json`
- Create: `src/Piggy3.Frontend/Piggy3.Frontend.csproj`, `src/Piggy3.Frontend/Program.cs`
- Create: `tests/Piggy3.Api.Tests/Piggy3.Api.Tests.csproj`, `tests/Piggy3.Api.Tests/MoneyTests.cs`

**Interfaces:**
- Produces: `Piggy3.Shared.Money.TryParse(string?, out decimal)`, `Money.Round(decimal)`, `Money.Format(decimal)`, `Money.Cents(decimal)`.
- Produces: all contract records listed below, used by every later task.

- [ ] **Step 1: Create the repository and projects**

```bash
cd /home/kaiwegner/Projects/piggy3
git init
dotnet new sln -n Piggy3
dotnet new classlib -o src/Piggy3.Shared -n Piggy3.Shared -f net10.0
dotnet new webapi -o src/Piggy3.Api -n Piggy3.Api -f net10.0 --use-controllers
dotnet new web -o src/Piggy3.Frontend -n Piggy3.Frontend -f net10.0
dotnet new xunit -o tests/Piggy3.Api.Tests -n Piggy3.Api.Tests -f net10.0
dotnet sln add src/Piggy3.Shared src/Piggy3.Api src/Piggy3.Frontend tests/Piggy3.Api.Tests
dotnet add src/Piggy3.Api reference src/Piggy3.Shared
dotnet add src/Piggy3.Frontend reference src/Piggy3.Shared
dotnet add tests/Piggy3.Api.Tests reference src/Piggy3.Api src/Piggy3.Shared
rm -f src/Piggy3.Api/Piggy3.Api.http
```

Write `.gitignore`:

```gitignore
bin/
obj/
.env
node_modules/
tests/e2e/test-results/
tests/e2e/playwright-report/
```

Write `.dockerignore`:

```dockerignore
**/bin
**/obj
.git
.env
node_modules
tests
```

- [ ] **Step 2: Add the pinned packages**

```bash
dotnet add src/Piggy3.Api package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.14
dotnet add src/Piggy3.Api package MongoDB.Driver --version 2.28.0
dotnet add tests/Piggy3.Api.Tests package Microsoft.AspNetCore.Mvc.Testing --version 10.0.14
dotnet add tests/Piggy3.Api.Tests package coverlet.collector --version 6.0.4
```

- [ ] **Step 3: Write the failing money tests**

Create `tests/Piggy3.Api.Tests/MoneyTests.cs`:

```csharp
using Piggy3.Shared;

namespace Piggy3.Api.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData("53,80", 53.80)]
    [InlineData("-53,80", -53.80)]
    [InlineData("53.80", 53.80)]
    [InlineData(" 1 234,56 € ", 1234.56)]
    [InlineData("0", 0)]
    [InlineData("53,805", 53.81)]
    [InlineData("-0,005", -0.01)]
    public void TryParse_ReturnsRoundedAmount(string input, decimal expected)
    {
        Assert.True(Money.TryParse(input, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1,2,3")]
    [InlineData("1.234,56")]
    [InlineData("12.")]
    public void TryParse_RejectsInvalidInput(string input)
    {
        Assert.False(Money.TryParse(input, out _));
    }

    [Fact]
    public void Format_UsesGermanNotationAndEuro()
    {
        Assert.Equal("1.234,56 €", Money.Format(1234.56m));
        Assert.Equal("-53,80 €", Money.Format(-53.80m));
        Assert.Equal("0,00 €", Money.Format(0m));
    }

    [Theory]
    [InlineData("53,80", 5380)]
    [InlineData("-53,80", -5380)]
    [InlineData("53,805", 5381)]
    public void Cents_RoundsToBankCents(string input, long expected)
    {
        Assert.True(Money.TryParse(input, out var amount));
        Assert.Equal(expected, Money.Cents(amount));
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test tests/Piggy3.Api.Tests --filter MoneyTests`
Expected: FAIL — `Money` does not exist.

- [ ] **Step 5: Implement `Money` and the contracts**

`src/Piggy3.Shared/Money.cs`:

```csharp
using System.Globalization;

namespace Piggy3.Shared;

public static class Money
{
    private const NumberStyles Styles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public static bool TryParse(string? input, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var text = input.Trim()
            .Replace("€", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty);

        if (text.Contains(',') && text.Contains('.'))
            return false;

        text = text.Replace(',', '.');
        if (text.Count(c => c == '.') > 1)
            return false;

        if (!decimal.TryParse(text, Styles, CultureInfo.InvariantCulture, out var parsed))
            return false;

        amount = Round(parsed);
        return true;
    }

    public static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static long Cents(decimal value) =>
        (long)Math.Round(value * 100m, MidpointRounding.AwayFromZero);

    public static string Format(decimal amount) =>
        amount.ToString("N2", CultureInfo.GetCultureInfo("de-DE")) + " €";
}
```

`src/Piggy3.Shared/Contracts.cs`:

```csharp
namespace Piggy3.Shared;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token);

public record AccountRow(string Id, string Name, string Type, decimal Sum);

public record MonthlyCostRow(string AccountId, string Name, string Type, decimal Value, bool IsDeleted);
public record MonthlyCostRequest(string Name, string Type, string Value);

public record TransactionRow(string Id, string Date, decimal Amount, string AccountId, string AccountName);
public record TransactionListResponse(IReadOnlyList<TransactionRow> Items, decimal Total);
public record TransactionRequest(string Date, string Amount, string AccountId);
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Piggy3.Api.Tests`
Expected: PASS (0 failures).

- [ ] **Step 7: Commit**

```bash
git add .gitignore .dockerignore Piggy3.sln src tests
git commit -m "feat: scaffold solution with shared money parsing and contracts"
```

---

### Task 2: API data model, Mongo repositories, JWT auth

**Files:**
- Create: `src/Piggy3.Api/Models/Account.cs`, `MonthlyCost.cs`, `Transaction.cs`
- Create: `src/Piggy3.Api/Data/IMongoContext.cs`, `MongoContext.cs`
- Create: `src/Piggy3.Api/Data/IAccountRepository.cs`, `AccountRepository.cs`
- Create: `src/Piggy3.Api/Data/IMonthlyCostRepository.cs`, `MonthlyCostRepository.cs`
- Create: `src/Piggy3.Api/Data/ITransactionRepository.cs`, `TransactionRepository.cs`
- Create: `src/Piggy3.Api/Auth/AccountTypes.cs`, `PasswordVerifier.cs`, `JwtTokenService.cs`
- Create: `src/Piggy3.Api/Controllers/AuthController.cs`, `AccountTypesController.cs`
- Modify: `src/Piggy3.Api/Program.cs`
- Modify: `src/Piggy3.Api/appsettings.json`
- Test: `tests/Piggy3.Api.Tests/AuthTests.cs`

**Interfaces:**
- Produces: `IAccountRepository.GetAllAsync(bool includeDeleted = false)`, `GetByIdAsync(string)`, `CreateAsync(Account)`, `UpdateAsync(Account)`.
- Produces: `IMonthlyCostRepository.GetAllAsync()`, `GetByAccountIdAsync(string)`, `CreateAsync(MonthlyCost)`, `UpdateAsync(MonthlyCost)`.
- Produces: `ITransactionRepository.GetAllAsync()`, `GetByIdAsync(string)`, `CreateAsync(Transaction)`, `UpdateAsync(Transaction)`, `DeleteAsync(string)`.
- Produces: `AccountTypes.Types` (`IReadOnlyList<string>`) and `AccountTypes.Contains(string)`.
- Produces: `PasswordVerifier.IsValid(string? username, string? password)`.
- Produces: `JwtTokenService.CreateToken(string username)`.
- Consumes: `Piggy3.Shared.LoginRequest`, `LoginResponse`.

- [ ] **Step 1: Write the failing auth tests**

Create `tests/Piggy3.Api.Tests/AuthTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Piggy3.Api.Auth;
using Piggy3.Api.Controllers;
using Piggy3.Shared;

namespace Piggy3.Api.Tests;

public class AuthTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static IConfiguration ValidConfig() => Config(
        ("AUTH_USERNAME", "kai"),
        ("AUTH_PASSWORD", "secret"),
        ("AUTH_JWT_SECRET", "0123456789abcdef0123456789abcdef"));

    [Fact]
    public void PasswordVerifier_AcceptsExactCredentials()
    {
        Assert.True(new PasswordVerifier(ValidConfig()).IsValid("kai", "secret"));
    }

    [Theory]
    [InlineData("kai", "wrong")]
    [InlineData("other", "secret")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void PasswordVerifier_RejectsWrongCredentials(string? user, string? password)
    {
        Assert.False(new PasswordVerifier(ValidConfig()).IsValid(user, password));
    }

    [Fact]
    public void JwtTokenService_CreatesTokenWithSubjectAndSevenDayExpiry()
    {
        var service = new JwtTokenService(ValidConfig());
        var token = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken("kai"));

        Assert.Equal("kai", token.Subject);
        Assert.InRange((token.ValidTo - DateTime.UtcNow).TotalDays, 6.9, 7.1);
    }

    [Fact]
    public async Task Login_ReturnsToken_ForValidCredentials()
    {
        var controller = new AuthController(
            new PasswordVerifier(ValidConfig()),
            new JwtTokenService(ValidConfig()));

        var result = await controller.Login(new LoginRequest("kai", "secret"));

        var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForBadCredentials()
    {
        var controller = new AuthController(
            new PasswordVerifier(ValidConfig()),
            new JwtTokenService(ValidConfig()));

        var result = await controller.Login(new LoginRequest("kai", "nope"));

        Assert.IsType<Microsoft.AspNetCore.Mvc.UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ApiRoutes_RequireAuthentication_ButLoginIsOpen()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("AUTH_USERNAME", "kai")
                .UseSetting("AUTH_PASSWORD", "secret")
                .UseSetting("AUTH_JWT_SECRET", "0123456789abcdef0123456789abcdef")
                .UseSetting("MONGO_CONNECTION_STRING", "mongodb://localhost:27017"));
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/account-types")).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("kai", "secret"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Token;

        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var types = await client.GetAsync("/api/account-types");
        Assert.Equal(HttpStatusCode.OK, types.StatusCode);
        Assert.Contains("Ausgabe", await types.Content.ReadAsStringAsync());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Piggy3.Api.Tests --filter AuthTests`
Expected: FAIL — auth types/controllers do not exist.

- [ ] **Step 3: Add models, Mongo context and repositories**

Create the three models with these exact members and BSON element names:

```csharp
// Account: id, name, type, isDeleted (bool), createdAt, updatedAt (DateTime UTC)
// MonthlyCost: id, accountId (string), value (decimal), createdAt, updatedAt
// Transaction: id, date (DateTime), amount (decimal), accountId (string), createdAt, updatedAt
```

Use `[BsonId]` + `[BsonRepresentation(BsonType.ObjectId)]` on every `Id`, and `[BsonElement("camelCase")]` on every property. Default timestamps to `DateTime.UtcNow`.

`IMongoContext` exposes `Accounts`, `MonthlyCosts`, `Transactions`. `MongoContext` gets them from `IMongoClient.GetDatabase(databaseName)` using collection names `accounts`, `monthlyCosts`, `transactions`. `CreateAsync` sets `CreatedAt`/`UpdatedAt`; `UpdateAsync` sets `UpdatedAt`. `GetAllAsync(includeDeleted)` branches on the boolean (do not capture it inside the Mongo filter). `GetByIdAsync` returns deleted accounts too.

- [ ] **Step 4: Implement auth and the auth controllers**

`AccountTypes` reads `configuration["ACCOUNT_TYPES"] ?? "Ausgabe,Sparen,Haus"`, splits on `,` with `RemoveEmptyEntries | TrimEntries`, preserves order, and `Contains` uses `StringComparer.Ordinal`.

`PasswordVerifier` reads `AUTH_USERNAME` and `AUTH_PASSWORD`; empty configured values always fail. Compare both values with SHA-256 hashes via `CryptographicOperations.FixedTimeEquals` so lengths and timing leak nothing.

`JwtTokenService` reads `AUTH_JWT_SECRET`, throws `InvalidOperationException` when it is shorter than 32 chars, and creates an HS256 token with issuer/audience `piggy3`, subject `username`, and 7-day expiry.

`AuthController` (`[ApiController]`, `[Route("api/auth")]`, `[AllowAnonymous]` on the action): `POST login` returns `Ok(new LoginResponse(token))` or `Unauthorized()`. `AccountTypesController` (`[Route("api/account-types")]`) returns `Ok(types.Types)`.

- [ ] **Step 5: Wire `Program.cs`**

Register `IMongoClient`, `IMongoContext`, the three repositories, `AccountTypes`, `PasswordVerifier`, `JwtTokenService`, controllers. Add JWT bearer authentication (validate issuer, audience, lifetime, signing key from `AUTH_JWT_SECRET`) and an authorization fallback policy requiring an authenticated user. Add `public partial class Program { }` at the end for `WebApplicationFactory`. Use `MONGO_CONNECTION_STRING` (default `mongodb://localhost:27017`) and `MONGO_DATABASE_NAME` (default `piggy3`).

- [ ] **Step 6: Run the auth tests to verify they pass**

Run: `dotnet test tests/Piggy3.Api.Tests --filter AuthTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Piggy3.Api tests/Piggy3.Api.Tests/AuthTests.cs
git commit -m "feat: add mongodb data layer and jwt authentication"
```

---

### Task 3: Accounts endpoint and test fakes

**Files:**
- Create: `src/Piggy3.Api/Controllers/AccountsController.cs`
- Create: `tests/Piggy3.Api.Tests/Fakes.cs`
- Test: `tests/Piggy3.Api.Tests/AccountsControllerTests.cs`

**Interfaces:**
- Consumes: `IAccountRepository`, `ITransactionRepository`, `AccountTypes`.
- Produces: `GET /api/accounts` returning `IReadOnlyList<AccountRow>`.
- Produces test fakes: `FakeAccountRepository`, `FakeMonthlyCostRepository`, `FakeTransactionRepository` implementing the repository interfaces with public `List<T>` backing stores.

- [ ] **Step 1: Write the test fakes**

`Fakes.cs` implements all three repository interfaces. `CreateAsync` assigns `ObjectId.GenerateNewId().ToString()` when `Id` is empty. `UpdateAsync` replaces the item with the same `Id`. `FakeAccountRepository.GetAllAsync(includeDeleted)` filters `!IsDeleted` unless included. `FakeMonthlyCostRepository.GetByAccountIdAsync` returns the first matching row. `FakeTransactionRepository.DeleteAsync` removes the row. Also add:

```csharp
public static class TestConfig
{
    public static AccountTypes Types(params string[] types) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ACCOUNT_TYPES"] = string.Join(',', types) }).Build());
}
```

- [ ] **Step 2: Write the failing accounts tests**

`AccountsControllerTests.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Piggy3.Api.Controllers;
using Piggy3.Api.Models;
using Piggy3.Shared;

namespace Piggy3.Api.Tests;

public class AccountsControllerTests
{
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeTransactionRepository _transactions = new();

    private AccountsController Controller() =>
        new(_accounts, _transactions, TestConfig.Types("Ausgabe", "Sparen"));

    [Fact]
    public async Task Get_ComputesNetSumPerActiveAccount()
    {
        var a = new Account { Name = "Internet", Type = "Ausgabe" };
        var b = new Account { Name = "Urlaub", Type = "Sparen" };
        _accounts.Accounts.AddRange([a, b]);
        _transactions.Transactions.AddRange([
            new Transaction { AccountId = a.Id, Amount = -53.80m },
            new Transaction { AccountId = a.Id, Amount = 3.80m },
            new Transaction { AccountId = b.Id, Amount = 100m }
        ]);

        var result = await Controller().Get();
        var rows = (IReadOnlyList<AccountRow>)((OkObjectResult)result.Result!).Value!;

        Assert.Equal(-50.00m, rows.Single(r => r.Name == "Internet").Sum);
        Assert.Equal(100m, rows.Single(r => r.Name == "Urlaub").Sum);
    }

    [Fact]
    public async Task Get_ExcludesDeletedAccounts()
    {
        _accounts.Accounts.Add(new Account { Name = "Alt", Type = "Ausgabe", IsDeleted = true });

        var result = await Controller().Get();
        var rows = (IReadOnlyList<AccountRow>)((OkObjectResult)result.Result!).Value!;

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Get_SortsByConfiguredTypeOrder_ThenName_AndPutsUnknownTypesLast()
    {
        _accounts.Accounts.AddRange([
            new Account { Name = "Zeta", Type = "Ausgabe" },
            new Account { Name = "Alpha", Type = "Ausgabe" },
            new Account { Name = "Sparen", Type = "Sparen" },
            new Account { Name = "Haus", Type = "Haus" }
        ]);

        var result = await Controller().Get();
        var rows = (IReadOnlyList<AccountRow>)((OkObjectResult)result.Result!).Value!;

        Assert.Equal(["Alpha", "Zeta", "Sparen", "Haus"], rows.Select(r => r.Name));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Piggy3.Api.Tests --filter AccountsControllerTests`
Expected: FAIL — `AccountsController` does not exist.

- [ ] **Step 4: Implement `AccountsController.Get`**

Load `_accounts.GetAllAsync()` and `_transactions.GetAllAsync()`, group transactions by `AccountId`, use `Money.Round` on each sum, and sort with configured type index first (unknown → `int.MaxValue`), then `StringComparer.Ordinal` on name. Return `Ok(rows)`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Piggy3.Api.Tests --filter AccountsControllerTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Piggy3.Api/Controllers/AccountsController.cs tests/Piggy3.Api.Tests/Fakes.cs tests/Piggy3.Api.Tests/AccountsControllerTests.cs
git commit -m "feat: add accounts endpoint with net sums"
```

---

### Task 4: Monthly costs endpoints (add, edit, soft-delete, restore)

**Files:**
- Create: `src/Piggy3.Api/Controllers/MonthlyCostsController.cs`
- Test: `tests/Piggy3.Api.Tests/MonthlyCostsControllerTests.cs`

**Interfaces:**
- Consumes: `IAccountRepository`, `IMonthlyCostRepository`, `AccountTypes`, `Piggy3.Shared.MonthlyCostRequest`, `MonthlyCostRow`.
- Produces: `GET /api/monthly-costs?includeDeleted=`, `POST /api/monthly-costs`, `PUT /api/monthly-costs/{accountId}`, `DELETE /api/monthly-costs/{accountId}`, `POST /api/monthly-costs/{accountId}/restore`.
- Produces: all mutations return `MonthlyCostRow` on success (restore returns `NoContent`).

- [ ] **Step 1: Write the failing tests**

Cover exactly these cases in `MonthlyCostsControllerTests`:

```csharp
// Get_JoinsActiveRows_SortedByTypeOrderThenName
// Get_IncludesDeletedOnlyWhenRequested
// Create_TrimsName_ParsesValue_AndCreatesAccountPlusCost
// Create_RejectsDuplicateActiveName_With400
// Create_RejectsUnknownType_With400
// Create_RejectsInvalidValue_With400
// Update_RenamesAccount_AndUpdatesValue_KeepingId
// Update_AllowsKeepingATypeThatWasRemovedFromConfig
// Update_RejectsChangingToUnknownType_With400
// Delete_SoftDeletesAccount_WithoutRemovingCost
// Restore_ClearsSoftDelete
// Restore_RejectsNameTakenByActiveAccount_With400_AndKeepsAccountDeleted
```

Assertions to pin: create with `(" internet nord ", "Ausgabe", "53,80")` stores name `internet nord`, value `53.80m`, type `Ausgabe`; invalid/duplicate/unknown type return `BadRequestObjectResult`; delete sets `IsDeleted == true` and leaves the monthly cost in the fake; restore collision leaves `IsDeleted == true`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Piggy3.Api.Tests --filter MonthlyCostsControllerTests`
Expected: FAIL — controller does not exist.

- [ ] **Step 3: Implement the controller**

Rules, in order for create: trim name, reject empty; `Money.TryParse` with `Money.Round`; require `_types.Contains(type)`; reject a name equal to any active account (`StringComparer.Ordinal`). Create the account and its monthly cost.

Update: load account by id, `404` if missing or deleted; trim/validate name and uniqueness excluding this id; allow `type == account.Type` even if no longer configured, otherwise require `_types.Contains(type)`; parse value; update account and cost (create the cost if absent).

Delete: load active account, `404` if missing, set `IsDeleted = true`, update, return `NoContent`.

Restore: load account, `404` if missing; if already active return `NoContent`; reject with `BadRequest` if any **active** account (other than this one) has the same name; otherwise clear the flag and return `NoContent`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Piggy3.Api.Tests --filter MonthlyCostsControllerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Piggy3.Api/Controllers/MonthlyCostsController.cs tests/Piggy3.Api.Tests/MonthlyCostsControllerTests.cs
git commit -m "feat: add monthly cost endpoints with soft delete and restore"
```

---

### Task 5: Transactions endpoints

**Files:**
- Create: `src/Piggy3.Api/Controllers/TransactionsController.cs`
- Test: `tests/Piggy3.Api.Tests/TransactionsControllerTests.cs`

**Interfaces:**
- Consumes: `ITransactionRepository`, `IAccountRepository`, `Piggy3.Shared.TransactionRequest`, `TransactionRow`, `TransactionListResponse`.
- Produces: `GET /api/transactions`, `POST /api/transactions`, `PUT /api/transactions/{id}`, `DELETE /api/transactions/{id}`.
- Produces: date format in responses is `yyyy-MM-dd`.

- [ ] **Step 1: Write the failing tests**

Cover exactly these cases:

```csharp
// Get_ReturnsRowsNewestFirst_AndNetTotal
// Get_IncludesBookingsOfDeletedAccounts_WithAccountName
// Create_ParsesGermanAmount_AndDate_AndRoundsToCents
// Create_StoresNegativeExpenses
// Create_RejectsUnknownAccount_With404
// Create_RejectsInvalidAmountOrDate_With400
// Update_ChangesDateAmountAndAccount
// Update_RejectsUnknownTransaction_With404
// Delete_RemovesTransaction_AndReturns204
```

Pin: creating `("2026-11-05", "-53,805", accountId)` stores amount `-53.81m` at `2026-11-05 00:00:00Z`; `Get` total for `-53.80` and `+10` is `-43.80`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Piggy3.Api.Tests --filter TransactionsControllerTests`
Expected: FAIL — controller does not exist.

- [ ] **Step 3: Implement the controller**

`Get`: load all transactions and all accounts including deleted; join names (fall back to empty name if an account id is somehow missing); sort by `Date` descending then `CreatedAt` descending; total = `Money.Round(sum)`; return `TransactionListResponse`.

`Create`/`Update`: parse `Date` with `DateOnly.TryParseExact("yyyy-MM-dd", InvariantCulture)`, store `DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)`; parse amount with `Money.TryParse`; require the account to exist (deleted allowed); `404` for missing transaction/account, `400` for bad input. Return the created/updated `TransactionRow` with account name.

`Delete`: `404` if missing, otherwise delete and return `NoContent`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Piggy3.Api.Tests --filter TransactionsControllerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Piggy3.Api/Controllers/TransactionsController.cs tests/Piggy3.Api.Tests/TransactionsControllerTests.cs
git commit -m "feat: add transaction endpoints with net totals"
```

---

### Task 6: Frontend authentication and shell

**Files:**
- Create: `src/Piggy3.Frontend/Services/ApiClient.cs`, `ApiException.cs`
- Create: `src/Piggy3.Frontend/Pages/_ViewImports.cshtml`, `_ViewStart.cshtml`
- Create: `src/Piggy3.Frontend/Pages/Shared/_Layout.cshtml`
- Create: `src/Piggy3.Frontend/Pages/Login.cshtml`, `Login.cshtml.cs`
- Create: `src/Piggy3.Frontend/Pages/Index.cshtml`, `Index.cshtml.cs` (skeleton)
- Create: `src/Piggy3.Frontend/wwwroot/css/site.css`
- Modify: `src/Piggy3.Frontend/Program.cs`, `appsettings.json`

**Interfaces:**
- Consumes: `Piggy3.Shared.LoginRequest`, `LoginResponse`.
- Produces: `ApiClient.LoginAsync(string username, string password)`, `GetAccountsAsync()`, `GetAccountTypesAsync()`, `GetMonthlyCostsAsync(bool includeDeleted)`, `CreateMonthlyCostAsync(MonthlyCostRequest)`, `UpdateMonthlyCostAsync(string accountId, MonthlyCostRequest)`, `DeleteMonthlyCostAsync(string accountId)`, `RestoreMonthlyCostAsync(string accountId)`, `GetTransactionsAsync()`, `CreateTransactionAsync(TransactionRequest)`, `UpdateTransactionAsync(string id, TransactionRequest)`, `DeleteTransactionAsync(string id)`.
- Produces: `ApiException` with `StatusCode` and `Message`.
- Produces: claim type `api_token` holding the API JWT.

- [ ] **Step 1: Configure the frontend host**

`Program.cs`: Razor Pages; cookie authentication (login path `/Login`, access denied `/Login`, 7-day sliding cookie); authorization fallback policy requiring an authenticated user; `AddHttpContextAccessor`; typed `HttpClient` for `ApiClient` with base address from `API_BASE_URL` (default `http://localhost:5080`); `app.UseStaticFiles()`, routing, authentication, authorization; `MapRazorPages()`; `public partial class Program { }`.

`appsettings.json`: logging plus `"AllowedHosts": "*"`.

- [ ] **Step 2: Implement `ApiClient`**

Use `System.Net.Http.Json` and `JsonSerializerDefaults.Web`. Attach `Authorization: Bearer <token>` from `IHttpContextAccessor.HttpContext?.User.FindFirst("api_token")?.Value` when present. `LoginAsync` posts `LoginRequest` without a token and returns `null` on `401`; other non-2xx responses throw `ApiException(status, body)`. All list/get methods return the shared contract types.

- [ ] **Step 3: Write the layout, login page and shell**

`_Layout.cshtml` (German, Bootstrap 5.3.3 + Bootstrap Icons via the same jsDelivr CDN URLs used by `../piggy`, `lang="de"`): navbar with brand "Piggy", the logged-in user name, and a POST logout form `asp-page="/Index" asp-page-handler="Logout"`. Render the body and optional Scripts section. Add `site.css` with money colouring (`.text-negative { color: #dc3545; }`).

`Login.cshtml`: centered card, bind `Username` and `Password`, error alert, submit button "Anmelden". `Login.cshtml.cs`: `[AllowAnonymous]`; `OnGet` redirects to `/` when already signed in. `OnPostAsync` calls `ApiClient.LoginAsync`, shows "Benutzername oder Passwort falsch." on failure, otherwise builds a `ClaimsIdentity` with `Name` and `api_token`, `SignInAsync("Cookies", ...)`, redirects to `/`.

`Index.cshtml.cs` skeleton: `[Authorize]`, `OnGet` returns `Page()`, `OnPostLogoutAsync` signs out and redirects to `/Login`. `Index.cshtml`: page title "Piggy" and a placeholder container.

- [ ] **Step 4: Build and smoke-check the shell**

Run: `dotnet build src/Piggy3.Frontend`
Expected: build succeeds.

Start the API with `AUTH_USERNAME=kai AUTH_PASSWORD=secret AUTH_JWT_SECRET=0123456789abcdef0123456789abcdef MONGO_CONNECTION_STRING=mongodb://localhost:27017 dotnet run --project src/Piggy3.Api --urls http://localhost:5080`, start the frontend with `API_BASE_URL=http://localhost:5080 dotnet run --project src/Piggy3.Frontend --urls http://localhost:5090`, then:

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5090/Login   # 200
curl -s -o /dev/null -w "%{http_code}" http://localhost:5090/        # 302
```

- [ ] **Step 5: Commit**

```bash
git add src/Piggy3.Frontend
git commit -m "feat: add frontend cookie auth and bootstrap shell"
```

---

### Task 7: Frontend tabs 1 and 2 (read, add, edit, soft-delete, restore)

**Files:**
- Modify: `src/Piggy3.Frontend/Pages/Index.cshtml.cs`
- Modify: `src/Piggy3.Frontend/Pages/Index.cshtml`
- Test: extended later by Task 10 (e2e)

**Interfaces:**
- Consumes: all `ApiClient` methods from Task 6.
- Produces: `IndexModel.ActiveTab` (`konten|kosten|buchungen`), `AccountGroups`, `MonthlyCosts`, `MonthlyTypeTotals`, `MonthlyGrandTotal`, `ShowDeleted`, `AccountTypes`, `AccountOptions`, `Transactions`, `TransactionTotal`, `DefaultDate`, `ErrorMessage`.
- Produces handlers: `OnPostAddMonthlyCostAsync(name, type, value)`, `OnPostUpdateMonthlyCostAsync(accountId, name, type, value)`, `OnPostDeleteMonthlyCostAsync(accountId)`, `OnPostRestoreMonthlyCostAsync(accountId)`.

- [ ] **Step 1: Implement the page model**

`OnGetAsync(string? tab, bool showDeleted = false)`:
- Normalize `tab` to one of the three values, default `konten`.
- Always load `AccountTypes` via the API.
- For `konten`: load accounts and group them by type in configured order, unknown types last; each row is `AccountRow`.
- For `kosten`: load monthly costs (`includeDeleted: showDeleted`); compute type totals in configured order plus unknown types, and the grand total; load account options from `/api/accounts`; keep only active costs in `ActiveMonthlyCosts` for the guessing JSON.
- For `buchungen`: load transactions and account options; set `DefaultDate` to `DateTime.Today.ToString("yyyy-MM-dd")`.
- Wrap API calls in `try/catch (ApiException)` and set `TempData["Error"]`.

Handlers call the matching API method with the raw string values. On success redirect with `RedirectToPage("/Index", new { tab = "kosten" })` (add/update) or `new { tab = "kosten", showDeleted = true }` (delete/restore). On `ApiException`, set `TempData["Error"]` to a short German message and redirect to the same tab.

- [ ] **Step 2: Render tab 1**

`Index.cshtml` renders Bootstrap `nav nav-tabs` links (`asp-route-tab="konten|kosten|buchungen"`). Only the active panel is rendered. Show `TempData["Error"]` as a dismissible `alert alert-danger`.

Tab 1: for each `AccountGroups` entry a heading with the type and a table `Konto | Typ | Summe`; sum rendered with `Money.Format` and `.text-negative` when below zero. Empty state "Noch keine Konten."

- [ ] **Step 3: Render tab 2**

Top summary: for each `MonthlyTypeTotals` a row, then a separator row "Total" with `MonthlyGrandTotal`, all using `Money.Format`.

Below, a table `Konto | Wert | Typ` where each row is editable in place:
- Inputs use the HTML `form` attribute pointing at a per-row form placed in the actions cell, e.g. `form="cost-@row.AccountId"`.
- Name text input, value text input (`row.Value.ToString("0.00", de-DE)`), type `<select>` containing configured types plus `row.Type` when it is not configured.
- The per-row form has hidden `accountId` and buttons: "Speichern" (`asp-page-handler="UpdateMonthlyCost"`), "Löschen" (`DeleteMonthlyCost`, `formnovalidate`) for active rows, or "Wiederherstellen" (`RestoreMonthlyCost`) for deleted rows.
- Deleted rows get `class="table-secondary"` and a "(gelöscht)" suffix.
- A checkbox/link "Gelöschte anzeigen" toggles `?tab=kosten&showDeleted=true`.

An add form below the table (`id="AddMonthlyCost"`) binds `NewName`, `NewValue`, `NewType` and posts `asp-page-handler="AddMonthlyCost"`.

- [ ] **Step 4: Build and verify manually**

Run: `dotnet build src/Piggy3.Frontend`
Expected: build succeeds.

With both apps running (from Task 6), log in and verify: create two costs in different types, the summary totals update, edit a name/value, delete a cost, tick "Gelöschte anzeigen", restore it. Tab 1 then shows the account with sum `0,00 €`.

- [ ] **Step 5: Commit**

```bash
git add src/Piggy3.Frontend/Pages/Index.cshtml src/Piggy3.Frontend/Pages/Index.cshtml.cs
git commit -m "feat: add account overview and monthly cost editing"
```

---

### Task 8: Frontend tab 3 (bookings, guessing, keyboard)

**Files:**
- Modify: `src/Piggy3.Frontend/Pages/Index.cshtml.cs`
- Modify: `src/Piggy3.Frontend/Pages/Index.cshtml`
- Create: `src/Piggy3.Frontend/wwwroot/js/app.js`
- Modify: `src/Piggy3.Frontend/Pages/Shared/_Layout.cshtml`

**Interfaces:**
- Produces handlers: `OnPostAddTransactionAsync(date, value, accountId)`, `OnPostUpdateTransactionAsync(id, date, value, accountId)`, `OnPostDeleteTransactionAsync(id)`.
- Produces DOM ids: `Date`, `Value`, `AccountId` for the add form; `monthly-costs` JSON script element.

- [ ] **Step 1: Implement the handlers**

Parse nothing in the page; forward the raw strings to the API. Add redirects to `new { tab = "buchungen" }`; on `ApiException` set `TempData["Error"]` and redirect to Buchungen. After add/update/delete the page reloads, so totals are recalculated from the API.

- [ ] **Step 2: Render the booking form and table**

Add form: `[Datum] [Wert] [Konto]`; date input `type="date"` bound to `DefaultDate`; text value input with `id="Value"` and `autofocus`; account `<select id="AccountId">` from active `AccountOptions`; submit button "Buchung hinzufügen" (Enter submits). Use `tabindex="1"` on value, `2` on account, `3` on date.

Below: "Gesamt: @Money.Format(Model.TransactionTotal)" and the table `Datum | Konto | Wert`, newest first, each row editable in place using the same per-row form + `form` attribute pattern as Task 7 (`id="tx-@row.Id"`): date input, amount text input, account select (active options plus the row's current account when it is deleted or missing, labelled "(gelöscht)"), "Speichern", "Löschen". Dates render as `dd.MM.yyyy` outside inputs.

Render the active monthly costs for guessing:

```html
<script id="monthly-costs" type="application/json">
@Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.ActiveMonthlyCosts.Select(c => new { c.AccountId, c.Value })))
</script>
```

Empty state: "Noch keine Buchungen."

- [ ] **Step 3: Implement `app.js`**

- On `DOMContentLoaded`, if `#Value` exists, call `focus()`.
- Parse money exactly like `Money.TryParse`: trim, remove `€`/spaces, reject both `,` and `.`, comma → dot, reject more than one dot, `Number.parseFloat`, return `Math.round(Math.abs(value) * 100)` or `null`.
- On `input` in `#Value`, compute cents; if exactly one active cost has the same absolute cents, set `#AccountId.value`; with zero or multiple matches leave it unchanged.
- On `keydown` in `#Date`, when Tab is pressed without Shift, prevent default and focus `#Value` (so value → account → date → value cycles). Enter is not intercepted, so the form submits.
- Include the script with `<script src="~/js/app.js"></script>` in `_Layout` (defer or place after Bootstrap).

- [ ] **Step 4: Verify the JS syntax and build**

Run: `node --check src/Piggy3.Frontend/wwwroot/js/app.js && dotnet build src/Piggy3.Frontend`
Expected: no output from `node --check`; build succeeds.

Manual check with both apps running: tab Buchungen focuses the value field; typing `-53,80` selects a `53,80` cost; Tab cycles through the three fields; Enter adds the row and updates the total; changing a row and saving works; deleting works.

- [ ] **Step 5: Commit**

```bash
git add src/Piggy3.Frontend
git commit -m "feat: add booking entry with exact-value account guessing"
```

---

### Task 9: Docker images and Compose stack

**Files:**
- Create: `src/Piggy3.Api/Dockerfile`
- Create: `src/Piggy3.Frontend/Dockerfile`
- Create: `docker-compose.yml`
- Create: `.env.example`
- Modify: `README.md` (created here with run instructions)

**Interfaces:**
- Consumes: `ACCOUNT_TYPES`, `AUTH_USERNAME`, `AUTH_PASSWORD`, `AUTH_JWT_SECRET`, `MONGO_CONNECTION_STRING`, `MONGO_DATABASE_NAME`, `API_BASE_URL`, `FRONTEND_PORT`.

- [ ] **Step 1: Write both Dockerfiles**

Both use `mcr.microsoft.com/dotnet/sdk:10.0` for build and `mcr.microsoft.com/dotnet/aspnet:10.0` for runtime, context is the repo root (Compose sets `context: .`). Copy the two csproj files first, restore, then copy the shared + app sources and publish. Runtime listens on `http://+:8080`, entrypoint is the app dll. API dll `Piggy3.Api.dll`, frontend `Piggy3.Frontend.dll`.

- [ ] **Step 2: Write Compose and the env example**

`docker-compose.yml`:

```yaml
services:
  mongo:
    image: mongo:7
    restart: unless-stopped
    volumes:
      - mongo-data:/data/db
    healthcheck:
      test: ["CMD", "mongosh", "--quiet", "--eval", "db.adminCommand('ping').ok"]
      interval: 5s
      timeout: 5s
      retries: 10

  api:
    build:
      context: .
      dockerfile: src/Piggy3.Api/Dockerfile
    restart: unless-stopped
    environment:
      - ACCOUNT_TYPES=${ACCOUNT_TYPES:-Ausgabe,Sparen,Haus}
      - AUTH_USERNAME=${AUTH_USERNAME:-admin}
      - AUTH_PASSWORD=${AUTH_PASSWORD:-admin}
      - AUTH_JWT_SECRET=${AUTH_JWT_SECRET:-change-me-32-chars-minimum-secret}
      - MONGO_CONNECTION_STRING=mongodb://mongo:27017
      - MONGO_DATABASE_NAME=${MONGO_DATABASE_NAME:-piggy3}
    depends_on:
      mongo:
        condition: service_healthy

  frontend:
    build:
      context: .
      dockerfile: src/Piggy3.Frontend/Dockerfile
    restart: unless-stopped
    environment:
      - API_BASE_URL=http://api:8080
    ports:
      - "${FRONTEND_PORT:-5000}:8080"
    depends_on:
      - api

volumes:
  mongo-data:
```

`.env.example` lists every variable with a short comment and safe placeholder values.

- [ ] **Step 3: Write the README**

German or English is fine; include: what it is, `cp .env.example .env`, `docker compose up -d --build`, open `http://localhost:5000`, configuration table, development commands, test commands, OIDC note (not implemented, frontend auth is the single seam).

- [ ] **Step 4: Verify the stack**

```bash
docker compose config > /dev/null
docker compose up -d --build
docker compose ps
curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/Login   # 200
curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/        # 302
```

Expected: three services running; both curl codes as noted. Leave the stack running for Task 10.

- [ ] **Step 5: Commit**

```bash
git add src/Piggy3.Api/Dockerfile src/Piggy3.Frontend/Dockerfile docker-compose.yml .env.example README.md
git commit -m "chore: add docker compose stack and run documentation"
```

---

### Task 10: Playwright end-to-end tests

**Files:**
- Create: `tests/e2e/package.json`, `playwright.config.ts`, `tests/piggy3.spec.ts`

**Interfaces:**
- Consumes: running stack at `BASE_URL` (default `http://localhost:5000`), `AUTH_USERNAME`/`AUTH_PASSWORD` (default `admin`/`admin`).
- Produces: serial Playwright suite.

- [ ] **Step 1: Scaffold Playwright**

```bash
cd tests/e2e
npm init -y
npm install --save-dev @playwright/test@1.64.1
npx playwright install chromium
```

`package.json` scripts: `"test": "playwright test"`, `"test:headed": "playwright test --headed"`.

`playwright.config.ts`: `testDir: './tests'`, `fullyParallel: false`, `workers: 1`, `use: { baseURL: process.env.BASE_URL ?? 'http://localhost:5000', trace: 'on-first-retry' }`, chromium project.

- [ ] **Step 2: Write the end-to-end tests**

`tests/piggy3.spec.ts` with these tests:

```ts
// "requires login" — goto '/', expect URL /Login, then log in and see the three tabs.
// "tab 2 computes type totals, edits and restores" — add Ausgabe 10,00 and Sparen 20,00,
//   expect type totals 10,00 € and 20,00 € and total 30,00 €; edit the Ausgabe row to 15,00;
//   delete the Sparen row; enable "Gelöschte anzeigen"; restore it; expect total 35,00 €.
// "tab 1 groups accounts and shows net sums" — add accounts, add bookings -5,00 and +2,00,
//   open Konten and expect the two rows with -5,00 € and 2,00 € in the configured type groups.
// "tab 3 guesses by absolute value, cycles focus and adds with Enter" — add a cost
//   "Internet-<suffix>" 53,80, open Buchungen, expect #Value focused, type -53,80,
//   expect #AccountId to have that account selected, press Tab (account focused),
//   Tab (date focused), Tab (value focused), press Enter, expect the row and total -53,80 €.
// "tab 3 edits and deletes a booking in place" — add a booking, change its value to 12,34,
//   save, expect 12,34 €, delete it and expect "Noch keine Buchungen."
// "empty state" — with a unique fresh run, tab 1 shows "Noch keine Konten." and 0,00 € totals.
```

Use a module-level unique suffix (`Date.now().toString(36)`) in every account name so tests do not collide across runs. Add helpers `login(page)`, `addCost(page, name, type, value)`, `goTab(page, 'kosten')` that navigate via the visible tab links.

Selectors: prefer the stable ids from Tasks 7/8 (`#NewName`, `#NewValue`, `#NewType`, `#AddMonthlyCost`, `#Date`, `#Value`, `#AccountId`) and accessible button names in German. Assert money text with `toContainText`.

- [ ] **Step 3: Run the suite against the stack**

```bash
cd tests/e2e
BASE_URL=http://localhost:5000 AUTH_USERNAME=admin AUTH_PASSWORD=admin npx playwright test
```

Expected: all tests pass. If the stack was started with different `.env` credentials, use those values.

- [ ] **Step 4: Commit**

```bash
git add tests/e2e
git commit -m "test: add playwright end-to-end coverage for all three tabs"
```

---

### Task 11: Final verification and cleanup

**Files:**
- Modify: `README.md` if commands changed

- [ ] **Step 1: Run the full API test suite**

Run: `dotnet test tests/Piggy3.Api.Tests`
Expected: PASS, 0 failures, 0 skipped.

- [ ] **Step 2: Build everything**

Run: `dotnet build Piggy3.sln`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Rebuild and run the stack from scratch**

```bash
docker compose down -v
docker compose up -d --build
docker compose ps
```

Expected: `mongo`, `api`, `frontend` running. Open `http://localhost:5000`, log in with the `.env` credentials, and walk the three tabs once.

- [ ] **Step 4: Run e2e again against the clean stack**

```bash
cd tests/e2e
BASE_URL=http://localhost:5000 AUTH_USERNAME=admin AUTH_PASSWORD=admin npx playwright test
```

Expected: all tests pass.

- [ ] **Step 5: Commit any final documentation fix**

```bash
git add README.md
git commit -m "docs: finalize setup and verification steps"
```

---

## Self-Review Notes

- Spec coverage: three tabs (Tasks 7–8), types from env (Tasks 2, 7), tab 1 read-only grouped sums (Tasks 3, 7), tab 2 editable with type + grand totals (Tasks 4, 7), soft delete/restore (Task 4), tab 3 date/value/account, manual date entry, exact absolute guessing, keyboard/focus, Enter add, net total, table (Tasks 5, 8), in-place booking edit/delete (Task 8), negative amounts (Tasks 1, 5), Docker/Mongo (Task 9), separate projects (Task 1), Bootstrap (Tasks 6–8), e2e (Task 10), API unit tests (Tasks 1–5), username/password with OIDC seam (Tasks 2, 6).
- No task depends on a name it does not define; all handler names and API routes appear in Interfaces blocks.
- Review Focus items are pinned to tests in Tasks 1, 4, 5, 7/8 and 10.
