using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Piggy3.Frontend.Services;

public static class AuthCookie
{
    public static Task SignInAsync(HttpContext context, string username, string token)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, username),
            new Claim("api_token", token)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        return context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }
}
