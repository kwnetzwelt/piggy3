using MongoDB.Driver;
using Piggy3.Api.Models;

namespace Piggy3.Api.Data;

public interface IMongoContext
{
    IMongoCollection<Account> Accounts { get; }
    IMongoCollection<MonthlyCost> MonthlyCosts { get; }
    IMongoCollection<Transaction> Transactions { get; }
}
