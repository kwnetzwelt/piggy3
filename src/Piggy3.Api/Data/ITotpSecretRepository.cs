using Piggy3.Api.Models;

namespace Piggy3.Api.Data;

public interface ITotpSecretRepository
{
    Task<TotpSecret?> GetAsync(string userId);
    Task UpsertAsync(TotpSecret secret);
    Task DeleteAsync(string userId);
}
