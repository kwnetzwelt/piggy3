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
        var a = new Account { Id = "a1", Name = "Internet", Type = "Ausgabe" };
        var b = new Account { Id = "b1", Name = "Urlaub", Type = "Sparen" };
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
        _accounts.Accounts.Add(new Account { Id = "a1", Name = "Alt", Type = "Ausgabe", IsDeleted = true });

        var result = await Controller().Get();
        var rows = (IReadOnlyList<AccountRow>)((OkObjectResult)result.Result!).Value!;

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Get_ReturnsEmptyList_WhenThereAreNoAccounts()
    {
        var result = await Controller().Get();
        var rows = (IReadOnlyList<AccountRow>)((OkObjectResult)result.Result!).Value!;

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Get_SortsByConfiguredTypeOrder_ThenName_AndPutsUnknownTypesLast()
    {
        _accounts.Accounts.AddRange([
            new Account { Id = "a1", Name = "Zeta", Type = "Ausgabe" },
            new Account { Id = "a2", Name = "Alpha", Type = "Ausgabe" },
            new Account { Id = "a3", Name = "Sparen", Type = "Sparen" },
            new Account { Id = "a4", Name = "Haus", Type = "Haus" }
        ]);

        var result = await Controller().Get();
        var rows = (IReadOnlyList<AccountRow>)((OkObjectResult)result.Result!).Value!;

        Assert.Equal(["Alpha", "Zeta", "Sparen", "Haus"], rows.Select(r => r.Name));
    }
}
