using Piggy3.Api.Models;

namespace Piggy3.Api.Data;

public interface IMonthlyCostRepository
{
    Task<List<MonthlyCost>> GetAllAsync();
    Task<MonthlyCost?> GetByAccountIdAsync(string accountId);
    Task CreateAsync(MonthlyCost cost);
    Task UpdateAsync(MonthlyCost cost);
}
