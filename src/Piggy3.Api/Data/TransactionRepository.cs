using MongoDB.Driver;
using Piggy3.Api.Models;

namespace Piggy3.Api.Data;

public class TransactionRepository : ITransactionRepository
{
    private readonly IMongoCollection<Transaction> _transactions;

    public TransactionRepository(IMongoContext context)
    {
        _transactions = context.Transactions;
    }

    public async Task<List<Transaction>> GetAllAsync()
    {
        return await _transactions.Find(_ => true).ToListAsync();
    }

    public async Task<Transaction?> GetByIdAsync(string id)
    {
        return await _transactions.Find(t => t.Id == id).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(Transaction transaction)
    {
        transaction.CreatedAt = DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;
        await _transactions.InsertOneAsync(transaction);
    }

    public async Task UpdateAsync(Transaction transaction)
    {
        transaction.UpdatedAt = DateTime.UtcNow;
        await _transactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);
    }

    public async Task DeleteAsync(string id)
    {
        await _transactions.DeleteOneAsync(t => t.Id == id);
    }
}
