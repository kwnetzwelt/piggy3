using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Piggy3.Api.Models;

public class Transaction
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("date")]
    public DateTime Date { get; set; }

    [BsonElement("amount")]
    public decimal Amount { get; set; }

    [BsonElement("accountId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AccountId { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
