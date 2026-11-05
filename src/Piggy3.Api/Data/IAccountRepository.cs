using Piggy3.Api.Models;

namespace Piggy3.Api.Data;

public interface IAccountRepository
{
    Task<List<Account>> GetAllAsync(bool includeDeleted = false);
    Task<Account?> GetByIdAsync(string id);
    Task CreateAsync(Account account);
    Task UpdateAsync(Account account);
}
