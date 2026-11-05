using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piggy3.Frontend.Services;

namespace Piggy3.Frontend.Pages;

[AllowAnonymous]
public class TwoFactorModel : PageModel
{
    private const string ChallengeCookie = "piggy3_2fa";
    private const string ChallengeUserCookie = "piggy3_2fa_user";

    private readonly ApiClient _api;

    public TwoFactorModel(ApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public string Code { get; set; } = string.Empty;

    public string? Error { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Index");

        if (string.IsNullOrEmpty(Request.Cookies[ChallengeCookie]) ||
            string.IsNullOrEmpty(Request.Cookies[ChallengeUserCookie]))
            return RedirectToPage("/Login");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var challenge = Request.Cookies[ChallengeCookie];
        if (string.IsNullOrEmpty(challenge))
            return RedirectToPage("/Login");

        var login = await _api.VerifyTwoFactorAsync(challenge, Code);
        if (login?.Token is null)
        {
            Error = "Der Code ist ungültig.";
            return Page();
        }

        Response.Cookies.Delete(ChallengeCookie);
        Response.Cookies.Delete(ChallengeUserCookie);
        await AuthCookie.SignInAsync(HttpContext, login.Username, login.Token);
        return RedirectToPage("/Index");
    }
}
