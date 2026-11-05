using System.Security.Cryptography;
using System.Text;

namespace Piggy3.Api.Auth;

public class PasswordVerifier
{
    private readonly string _username;
    private readonly string _password;

    public PasswordVerifier(IConfiguration configuration)
    {
        _username = configuration["AUTH_USERNAME"] ?? string.Empty;
        _password = configuration["AUTH_PASSWORD"] ?? string.Empty;
    }

    public bool IsValid(string? username, string? password)
    {
        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password) ||
            username is null || password is null)
            return false;

        return FixedEquals(username, _username) && FixedEquals(password, _password);
    }

    private static bool FixedEquals(string left, string right)
    {
        var leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left));
        var rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }
}
