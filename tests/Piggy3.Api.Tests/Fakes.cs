using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using Piggy3.Api.Auth;
using Piggy3.Api.Data;
using Piggy3.Api.Models;

namespace Piggy3.Api.Tests;

public sealed class FakeAccountRepository : IAccountRepository
{
    public List<Account> Accounts { get; } = [];

    public Task<List<Account>> GetAllAsync(bool includeDeleted = false) =>
        Task.FromResult(Accounts.Where(a => includeDeleted || !a.IsDeleted).ToList());

    public Task<Account?> GetByIdAsync(string id) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Id == id));

    public Task CreateAsync(Account account)
    {
        if (string.IsNullOrEmpty(account.Id))
            account.Id = ObjectId.GenerateNewId().ToString();
        account.CreatedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        Accounts.Add(account);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Account account)
    {
        account.UpdatedAt = DateTime.UtcNow;
        var index = Accounts.FindIndex(a => a.Id == account.Id);
        if (index >= 0)
            Accounts[index] = account;
        return Task.CompletedTask;
    }
}

public sealed class FakeMonthlyCostRepository : IMonthlyCostRepository
{
    public List<MonthlyCost> Costs { get; } = [];

    public Task<List<MonthlyCost>> GetAllAsync() => Task.FromResult(Costs.ToList());

    public Task<MonthlyCost?> GetByAccountIdAsync(string accountId) =>
        Task.FromResult(Costs.FirstOrDefault(c => c.AccountId == accountId));

    public Task CreateAsync(MonthlyCost cost)
    {
        if (string.IsNullOrEmpty(cost.Id))
            cost.Id = ObjectId.GenerateNewId().ToString();
        cost.CreatedAt = DateTime.UtcNow;
        cost.UpdatedAt = DateTime.UtcNow;
        Costs.Add(cost);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MonthlyCost cost)
    {
        cost.UpdatedAt = DateTime.UtcNow;
        var index = Costs.FindIndex(c => c.Id == cost.Id);
        if (index >= 0)
            Costs[index] = cost;
        return Task.CompletedTask;
    }
}

public sealed class FakeTransactionRepository : ITransactionRepository{
    public List<Transaction> Transactions { get; } = [];

    public Task<List<Transaction>> GetAllAsync() => Task.FromResult(Transactions.ToList());

    public Task<Transaction?> GetByIdAsync(string id) =>
        Task.FromResult(Transactions.FirstOrDefault(t => t.Id == id));

    public Task CreateAsync(Transaction transaction)
    {
        if (string.IsNullOrEmpty(transaction.Id))
            transaction.Id = ObjectId.GenerateNewId().ToString();
        transaction.CreatedAt = DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;
        Transactions.Add(transaction);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Transaction transaction)
    {
        transaction.UpdatedAt = DateTime.UtcNow;
        var index = Transactions.FindIndex(t => t.Id == transaction.Id);
        if (index >= 0)
            Transactions[index] = transaction;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        Transactions.RemoveAll(t => t.Id == id);
        return Task.CompletedTask;
    }
}

public sealed class FakeTotpSecretRepository : ITotpSecretRepository
{
    public List<TotpSecret> Secrets { get; } = [];

    public Task<TotpSecret?> GetAsync(string userId) =>
        Task.FromResult(Secrets.FirstOrDefault(s => s.UserId == userId));

    public Task UpsertAsync(TotpSecret secret)
    {
        var index = Secrets.FindIndex(s => s.UserId == secret.UserId);
        if (index >= 0)
            Secrets[index] = secret;
        else
            Secrets.Add(secret);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string userId)
    {
        Secrets.RemoveAll(s => s.UserId == userId);
        return Task.CompletedTask;
    }
}

public static class TestConfig
{
    public static AccountTypes Types(params string[] types) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ACCOUNT_TYPES"] = string.Join(',', types) }).Build());
}
