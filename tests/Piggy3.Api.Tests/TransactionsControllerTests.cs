using Microsoft.AspNetCore.Mvc;
using Piggy3.Api.Controllers;
using Piggy3.Api.Models;
using Piggy3.Shared;

namespace Piggy3.Api.Tests;

public class TransactionsControllerTests
{
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeTransactionRepository _transactions = new();

    private TransactionsController Controller() => new(_transactions, _accounts);

    private Account AddAccount(string id, string name, bool deleted = false)
    {
        var account = new Account { Id = id, Name = name, Type = "Ausgabe", IsDeleted = deleted };
        _accounts.Accounts.Add(account);
        return account;
    }

    [Fact]
    public async Task Get_ReturnsRowsNewestFirst_AndNetTotal()
    {
        var account = AddAccount("a1", "Internet");
        _transactions.Transactions.AddRange([
            new Transaction
            {
                Id = "t1", AccountId = account.Id, Amount = -53.80m,
                Date = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc)
            },
            new Transaction
            {
                Id = "t2", AccountId = account.Id, Amount = 10m,
                Date = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc)
            }
        ]);

        var result = await Controller().Get();
        var response = Assert.IsType<TransactionListResponse>(((OkObjectResult)result.Result!).Value);

        Assert.Equal(["t2", "t1"], response.Items.Select(i => i.Id));
        Assert.Equal(-43.80m, response.Total);
    }

    [Fact]
    public async Task Get_IncludesBookingsOfDeletedAccounts_WithAccountName()
    {
        var account = AddAccount("a1", "Alt", deleted: true);
        _transactions.Transactions.Add(new Transaction
        {
            Id = "t1", AccountId = account.Id, Amount = -5m,
            Date = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var result = await Controller().Get();
        var response = Assert.IsType<TransactionListResponse>(((OkObjectResult)result.Result!).Value);

        Assert.Equal("Alt", response.Items.Single().AccountName);
        Assert.Equal(-5m, response.Total);
    }

    [Fact]
    public async Task Create_ParsesGermanAmount_AndDate_AndRoundsToCents()
    {
        var account = AddAccount("a1", "Internet");

        var result = await Controller().Create(
            new TransactionRequest("2026-11-05", "-53,805", account.Id));

        var row = Assert.IsType<TransactionRow>(((OkObjectResult)result.Result!).Value);
        Assert.Equal("2026-11-05", row.Date);
        Assert.Equal(-53.81m, row.Amount);
        Assert.Equal("Internet", row.AccountName);

        var stored = _transactions.Transactions.Single();
        Assert.Equal(-53.81m, stored.Amount);
        Assert.Equal(new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc), stored.Date);
        Assert.Equal(DateTimeKind.Utc, stored.Date.Kind);
    }

    [Fact]
    public async Task Create_StoresNegativeExpenses()
    {
        var account = AddAccount("a1", "Internet");

        var result = await Controller().Create(
            new TransactionRequest("2026-11-05", "-10,00", account.Id));

        var row = Assert.IsType<TransactionRow>(((OkObjectResult)result.Result!).Value);
        Assert.Equal(-10m, row.Amount);
        Assert.Equal(-10m, _transactions.Transactions.Single().Amount);
    }

    [Fact]
    public async Task Create_RejectsUnknownAccount_With404()
    {
        var result = await Controller().Create(
            new TransactionRequest("2026-11-05", "-10,00", "missing"));

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(_transactions.Transactions);
    }

    [Theory]
    [InlineData("2026-11-05", "abc")]
    [InlineData("05.11.2026", "-10,00")]
    [InlineData("", "-10,00")]
    public async Task Create_RejectsInvalidAmountOrDate_With400(string date, string amount)
    {
        var account = AddAccount("a1", "Internet");

        var result = await Controller().Create(new TransactionRequest(date, amount, account.Id));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(_transactions.Transactions);
    }

    [Fact]
    public async Task Update_ChangesDateAmountAndAccount()
    {
        AddAccount("a1", "Internet");
        AddAccount("a2", "Urlaub");
        _transactions.Transactions.Add(new Transaction
        {
            Id = "t1", AccountId = "a1", Amount = -10m,
            Date = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var result = await Controller().Update(
            "t1", new TransactionRequest("2026-12-24", "25,50", "a2"));

        var row = Assert.IsType<TransactionRow>(((OkObjectResult)result.Result!).Value);
        Assert.Equal("2026-12-24", row.Date);
        Assert.Equal(25.50m, row.Amount);
        Assert.Equal("a2", row.AccountId);
        Assert.Equal("Urlaub", row.AccountName);
    }

    [Fact]
    public async Task Update_RejectsUnknownTransaction_With404()
    {
        AddAccount("a1", "Internet");

        var result = await Controller().Update(
            "missing", new TransactionRequest("2026-11-05", "1,00", "a1"));

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_RemovesTransaction_AndReturns204()
    {
        AddAccount("a1", "Internet");
        _transactions.Transactions.Add(new Transaction { Id = "t1", AccountId = "a1", Amount = 1m });

        var result = await Controller().Delete("t1");

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(_transactions.Transactions);
    }
}
