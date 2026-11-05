using MongoDB.Driver;
using Piggy3.Api.Models;

namespace Piggy3.Api.Data;

public class MongoContext : IMongoContext
{
    private readonly IMongoDatabase _database;

    public MongoContext(IMongoClient client, string databaseName)
    {
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<Account> Accounts => _database.GetCollection<Account>("accounts");
    public IMongoCollection<MonthlyCost> MonthlyCosts => _database.GetCollection<MonthlyCost>("monthlyCosts");
    public IMongoCollection<Transaction> Transactions => _database.GetCollection<Transaction>("transactions");
}
