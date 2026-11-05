using MongoDB.Driver;
using Piggy3.Api.Models;

namespace Piggy3.Api.Data;

public class MonthlyCostRepository : IMonthlyCostRepository
{
    private readonly IMongoCollection<MonthlyCost> _costs;

    public MonthlyCostRepository(IMongoContext context)
    {
        _costs = context.MonthlyCosts;
    }

    public async Task<List<MonthlyCost>> GetAllAsync()
    {
        return await _costs.Find(_ => true).ToListAsync();
    }

    public async Task<MonthlyCost?> GetByAccountIdAsync(string accountId)
    {
        return await _costs.Find(c => c.AccountId == accountId).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(MonthlyCost cost)
    {
        cost.CreatedAt = DateTime.UtcNow;
        cost.UpdatedAt = DateTime.UtcNow;
        await _costs.InsertOneAsync(cost);
    }

    public async Task UpdateAsync(MonthlyCost cost)
    {
        cost.UpdatedAt = DateTime.UtcNow;
        await _costs.ReplaceOneAsync(c => c.Id == cost.Id, cost);
    }
}
