using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piggy3.Api.Auth;
using Piggy3.Shared;

namespace Piggy3.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly PasswordVerifier _passwords;
    private readonly JwtTokenService _tokens;

    public AuthController(PasswordVerifier passwords, JwtTokenService tokens)
    {
        _passwords = passwords;
        _tokens = tokens;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!_passwords.IsValid(request.Username, request.Password))
            return Task.FromResult<IActionResult>(Unauthorized());

        return Task.FromResult<IActionResult>(Ok(new LoginResponse(_tokens.CreateToken(request.Username))));
    }
}
