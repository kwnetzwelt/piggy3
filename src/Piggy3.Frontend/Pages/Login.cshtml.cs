using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piggy3.Frontend.Services;

namespace Piggy3.Frontend.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private const string ChallengeCookie = "piggy3_2fa";
    private const string ChallengeUserCookie = "piggy3_2fa_user";

    private readonly ApiClient _api;

    public LoginModel(ApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? Error { get; private set; }

    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? RedirectToPage("/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var login = await _api.LoginAsync(Username, Password);
        if (login is null)
        {
            Error = "Benutzername oder Passwort falsch.";
            return Page();
        }

        if (login.TwoFactorRequired)
        {
            var options = new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddMinutes(5)
            };

            Response.Cookies.Append(ChallengeCookie, login.ChallengeToken!, options);
            Response.Cookies.Append(ChallengeUserCookie, login.Username, options);
            return RedirectToPage("/TwoFactor");
        }

        await AuthCookie.SignInAsync(HttpContext, login.Username, login.Token!);
        return RedirectToPage("/Index");
    }
}
