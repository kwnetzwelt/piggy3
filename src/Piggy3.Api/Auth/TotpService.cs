using OtpNet;

namespace Piggy3.Api.Auth;

public class TotpService
{
    public string GenerateSecret() =>
        Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public string BuildUri(string username, string secret) =>
        $"otpauth://totp/Piggy3:{Uri.EscapeDataString(username)}?secret={secret}&issuer=Piggy3";

    public bool Verify(string secret, string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(secret))
            return false;

        try
        {
            var totp = new Totp(Base32Encoding.ToBytes(secret));
            return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(1, 1));
        }
        catch (Exception)
        {
            return false;
        }
    }
}
