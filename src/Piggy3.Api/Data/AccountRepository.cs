using MongoDB.Driver;
using Piggy3.Api.Models;

namespace Piggy3.Api.Data;

public class AccountRepository : IAccountRepository
{
    private readonly IMongoCollection<Account> _accounts;

    public AccountRepository(IMongoContext context)
    {
        _accounts = context.Accounts;
    }

    public async Task<List<Account>> GetAllAsync(bool includeDeleted = false)
    {
        return includeDeleted
            ? await _accounts.Find(_ => true).ToListAsync()
            : await _accounts.Find(a => !a.IsDeleted).ToListAsync();
    }

    public async Task<Account?> GetByIdAsync(string id)
    {
        return await _accounts.Find(a => a.Id == id).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(Account account)
    {
        account.CreatedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        await _accounts.InsertOneAsync(account);
    }

    public async Task UpdateAsync(Account account)
    {
        account.UpdatedAt = DateTime.UtcNow;
        await _accounts.ReplaceOneAsync(a => a.Id == account.Id, account);
    }
}
