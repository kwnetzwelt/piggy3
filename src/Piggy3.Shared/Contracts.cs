namespace Piggy3.Shared;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string? Token, string? ChallengeToken, bool TwoFactorRequired, string Username);

public record TwoFactorStatusResponse(bool Enabled);
public record TwoFactorSetupResponse(string Secret, string OtpauthUri);
public record TwoFactorEnableRequest(string Secret, string Code);
public record TwoFactorVerifyRequest(string ChallengeToken, string Code);

public record AccountRow(string Id, string Name, string Type, decimal Sum);

public record MonthlyCostRow(string AccountId, string Name, string Type, decimal Value, bool IsDeleted);
public record MonthlyCostRequest(string Name, string Type, string Value);

public record TransactionRow(string Id, string Date, decimal Amount, string AccountId, string AccountName);
public record TransactionListResponse(IReadOnlyList<TransactionRow> Items, decimal Total);
public record TransactionRequest(string Date, string Amount, string AccountId);
