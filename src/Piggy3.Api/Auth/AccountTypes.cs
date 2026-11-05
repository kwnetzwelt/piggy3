namespace Piggy3.Api.Auth;

public class AccountTypes
{
    public IReadOnlyList<string> Types { get; }

    public AccountTypes(IConfiguration configuration)
    {
        Types = (configuration["ACCOUNT_TYPES"] ?? "Ausgabe,Sparen,Haus")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public bool Contains(string type) => Types.Contains(type, StringComparer.Ordinal);
}
