using Microsoft.AspNetCore.Mvc;
using Piggy3.Api.Controllers;
using Piggy3.Api.Models;
using Piggy3.Shared;

namespace Piggy3.Api.Tests;

public class MonthlyCostsControllerTests
{
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeMonthlyCostRepository _costs = new();

    private MonthlyCostsController Controller() =>
        new(_accounts, _costs, TestConfig.Types("Ausgabe", "Sparen"));

    private (Account Account, MonthlyCost Cost) AddActive(
        string id, string name, string type, decimal value)
    {
        var account = new Account { Id = id, Name = name, Type = type };
        var cost = new MonthlyCost { Id = $"c-{id}", AccountId = id, Value = value };
        _accounts.Accounts.Add(account);
        _costs.Costs.Add(cost);
        return (account, cost);
    }

    [Fact]
    public async Task Get_JoinsActiveRows_SortedByTypeOrderThenName()
    {
        AddActive("a1", "Zeta", "Ausgabe", 1m);
        AddActive("a2", "Alpha", "Ausgabe", 2m);
        AddActive("a3", "Sparen", "Sparen", 3m);

        var result = await Controller().Get(includeDeleted: false);
        var rows = (IReadOnlyList<MonthlyCostRow>)((OkObjectResult)result.Result!).Value!;

        Assert.Equal(["Alpha", "Zeta", "Sparen"], rows.Select(r => r.Name));
        Assert.Equal(3m, rows.Single(r => r.Name == "Sparen").Value);
    }

    [Fact]
    public async Task Get_IncludesDeletedOnlyWhenRequested()
    {
        AddActive("a1", "Aktiv", "Ausgabe", 1m);
        AddActive("a2", "Alt", "Ausgabe", 2m).Account.IsDeleted = true;

        var active = (IReadOnlyList<MonthlyCostRow>)
            ((OkObjectResult)(await Controller().Get(includeDeleted: false)).Result!).Value!;
        var all = (IReadOnlyList<MonthlyCostRow>)
            ((OkObjectResult)(await Controller().Get(includeDeleted: true)).Result!).Value!;

        Assert.Equal(["Aktiv"], active.Select(r => r.Name));
        Assert.Equal(["Aktiv", "Alt"], all.Select(r => r.Name));
        Assert.True(all.Single(r => r.Name == "Alt").IsDeleted);
    }

    [Fact]
    public async Task Create_TrimsName_ParsesValue_AndCreatesAccountPlusCost()
    {
        var result = await Controller().Create(
            new MonthlyCostRequest(" internet nord ", "Ausgabe", "53,80"));

        var row = Assert.IsType<MonthlyCostRow>(((OkObjectResult)result.Result!).Value);
        Assert.Equal("internet nord", row.Name);
        Assert.Equal("Ausgabe", row.Type);
        Assert.Equal(53.80m, row.Value);
        Assert.Single(_accounts.Accounts);
        Assert.Single(_costs.Costs);
        Assert.Equal(_accounts.Accounts[0].Id, _costs.Costs[0].AccountId);
    }

    [Fact]
    public async Task Create_RejectsDuplicateActiveName_With400()
    {
        AddActive("a1", "Internet", "Ausgabe", 1m);

        var result = await Controller().Create(new MonthlyCostRequest("Internet", "Ausgabe", "2,00"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Single(_accounts.Accounts);
    }

    [Fact]
    public async Task Create_RejectsUnknownType_With400()
    {
        var result = await Controller().Create(new MonthlyCostRequest("Internet", "Haus", "2,00"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(_accounts.Accounts);
    }

    [Fact]
    public async Task Create_RejectsInvalidValue_With400()
    {
        var result = await Controller().Create(new MonthlyCostRequest("Internet", "Ausgabe", "abc"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(_accounts.Accounts);
    }

    [Fact]
    public async Task Update_RenamesAccount_AndUpdatesValue_KeepingId()
    {
        AddActive("a1", "Alt", "Ausgabe", 1m);

        var result = await Controller().Update("a1", new MonthlyCostRequest("Neu", "Ausgabe", "10,50"));

        var row = Assert.IsType<MonthlyCostRow>(((OkObjectResult)result.Result!).Value);
        Assert.Equal("a1", row.AccountId);
        Assert.Equal("Neu", row.Name);
        Assert.Equal(10.50m, row.Value);
        Assert.Equal("a1", _accounts.Accounts.Single().Id);
    }

    [Fact]
    public async Task Update_AllowsKeepingATypeThatWasRemovedFromConfig()
    {
        AddActive("a1", "Haushalt", "Haus", 1m);

        var result = await Controller().Update("a1", new MonthlyCostRequest("Haushalt", "Haus", "2,00"));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("Haus", _accounts.Accounts.Single().Type);
    }

    [Fact]
    public async Task Update_RejectsChangingToUnknownType_With400()
    {
        AddActive("a1", "Internet", "Ausgabe", 1m);

        var result = await Controller().Update("a1", new MonthlyCostRequest("Internet", "Haus", "2,00"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Ausgabe", _accounts.Accounts.Single().Type);
    }

    [Fact]
    public async Task Delete_SoftDeletesAccount_WithoutRemovingCost()
    {
        AddActive("a1", "Internet", "Ausgabe", 1m);

        var result = await Controller().Delete("a1");

        Assert.IsType<NoContentResult>(result);
        Assert.True(_accounts.Accounts.Single().IsDeleted);
        Assert.Single(_costs.Costs);
    }

    [Fact]
    public async Task Restore_ClearsSoftDelete()
    {
        AddActive("a1", "Internet", "Ausgabe", 1m).Account.IsDeleted = true;

        var result = await Controller().Restore("a1");

        Assert.IsType<NoContentResult>(result);
        Assert.False(_accounts.Accounts.Single().IsDeleted);
    }

    [Fact]
    public async Task Restore_RejectsNameTakenByActiveAccount_With400_AndKeepsAccountDeleted()
    {
        AddActive("a1", "Internet", "Ausgabe", 1m).Account.IsDeleted = true;
        AddActive("a2", "Internet", "Ausgabe", 2m);

        var result = await Controller().Restore("a1");

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.True(_accounts.Accounts.Single(a => a.Id == "a1").IsDeleted);
    }
}
