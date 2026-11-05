using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Piggy3.Api.Models;

public class TotpSecret
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("secret")]
    public string Secret { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
