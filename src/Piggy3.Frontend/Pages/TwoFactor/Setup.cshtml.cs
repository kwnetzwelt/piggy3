using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piggy3.Frontend.Services;
using QRCoder;

namespace Piggy3.Frontend.Pages.TwoFactor;

[Authorize]
public class SetupModel : PageModel
{
    private readonly ApiClient _api;

    public SetupModel(ApiClient api)
    {
        _api = api;
    }

    public bool Enabled { get; private set; }
    public string? Secret { get; private set; }
    public string? OtpauthUri { get; private set; }
    public string? QrCodeSvg { get; private set; }
    public string? Success { get; private set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostEnableAsync(string secret, string otpauthUri, string code)
    {
        try
        {
            await _api.EnableTwoFactorAsync(secret, code);
            await LoadAsync();
            Success = "Zwei-Faktor-Authentifizierung ist aktiv.";
        }
        catch (ApiException)
        {
            Enabled = false;
            Secret = secret;
            OtpauthUri = otpauthUri;
            QrCodeSvg = GenerateSvg(otpauthUri);
            Error = "Der Code ist ungültig. Bitte erneut versuchen.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDisableAsync()
    {
        await _api.DisableTwoFactorAsync();
        await LoadAsync();
        Success = "Zwei-Faktor-Authentifizierung ist deaktiviert.";
        return Page();
    }

    private async Task LoadAsync()
    {
        var status = await _api.GetTwoFactorStatusAsync();
        Enabled = status.Enabled;
        if (Enabled)
            return;

        var setup = await _api.SetupTwoFactorAsync();
        Secret = setup.Secret;
        OtpauthUri = setup.OtpauthUri;
        QrCodeSvg = GenerateSvg(setup.OtpauthUri);
    }

    private static string GenerateSvg(string otpauthUri)
    {
        using var qr = new QRCodeGenerator();
        var data = qr.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.Q);
        using var svg = new SvgQRCode(data);
        return svg.GetGraphic(4);
    }
}
