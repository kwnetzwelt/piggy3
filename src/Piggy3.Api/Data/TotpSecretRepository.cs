using MongoDB.Driver;
using Piggy3.Api.Models;

namespace Piggy3.Api.Data;

public class TotpSecretRepository : ITotpSecretRepository
{
    private readonly IMongoCollection<TotpSecret> _secrets;

    public TotpSecretRepository(IMongoContext context)
    {
        _secrets = context.TotpSecrets;
    }

    public async Task<TotpSecret?> GetAsync(string userId)
    {
        return await _secrets.Find(s => s.UserId == userId).FirstOrDefaultAsync();
    }

    public async Task UpsertAsync(TotpSecret secret)
    {
        if (string.IsNullOrEmpty(secret.Id))
            secret.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

        await _secrets.ReplaceOneAsync(
            s => s.UserId == secret.UserId,
            secret,
            new ReplaceOptions { IsUpsert = true });
    }

    public async Task DeleteAsync(string userId)
    {
        await _secrets.DeleteOneAsync(s => s.UserId == userId);
    }
}
