using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piggy3.Api.Auth;
using Piggy3.Api.Data;
using Piggy3.Api.Models;
using Piggy3.Shared;

namespace Piggy3.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly PasswordVerifier _passwords;
    private readonly JwtTokenService _tokens;
    private readonly ITotpSecretRepository _totpSecrets;
    private readonly TotpService _totp;

    public AuthController(
        PasswordVerifier passwords,
        JwtTokenService tokens,
        ITotpSecretRepository totpSecrets,
        TotpService totp)
    {
        _passwords = passwords;
        _tokens = tokens;
        _totpSecrets = totpSecrets;
        _totp = totp;
    }

    private string CurrentUser =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirst("sub")?.Value
        ?? User.Identity?.Name
        ?? string.Empty;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!_passwords.IsValid(request.Username, request.Password))
            return Unauthorized();

        var secret = await _totpSecrets.GetAsync(request.Username);
        if (secret is not null)
        {
            return Ok(new LoginResponse(
                null,
                _tokens.CreateChallengeToken(request.Username),
                true,
                request.Username));
        }

        return Ok(new LoginResponse(
            _tokens.CreateToken(request.Username), null, false, request.Username));
    }

    [HttpPost("2fa/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify([FromBody] TwoFactorVerifyRequest request)
    {
        if (!_tokens.TryValidateChallengeToken(request.ChallengeToken, out var username) ||
            username is null)
            return Unauthorized();

        var secret = await _totpSecrets.GetAsync(username);
        if (secret is null || !_totp.Verify(secret.Secret, request.Code))
            return Unauthorized();

        return Ok(new LoginResponse(_tokens.CreateToken(username), null, false, username));
    }

    [HttpGet("2fa")]
    public async Task<ActionResult<TwoFactorStatusResponse>> Status()
    {
        var secret = await _totpSecrets.GetAsync(CurrentUser);
        return Ok(new TwoFactorStatusResponse(secret is not null));
    }

    [HttpPost("2fa/setup")]
    public ActionResult<TwoFactorSetupResponse> Setup()
    {
        var secret = _totp.GenerateSecret();
        return Ok(new TwoFactorSetupResponse(secret, _totp.BuildUri(CurrentUser, secret)));
    }

    [HttpPost("2fa/enable")]
    public async Task<IActionResult> Enable([FromBody] TwoFactorEnableRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Secret) || !_totp.Verify(request.Secret, request.Code))
            return BadRequest("Code is invalid.");

        await _totpSecrets.UpsertAsync(new TotpSecret
        {
            UserId = CurrentUser,
            Secret = request.Secret,
            CreatedAt = DateTime.UtcNow
        });

        return NoContent();
    }

    [HttpDelete("2fa")]
    public async Task<IActionResult> Disable()
    {
        await _totpSecrets.DeleteAsync(CurrentUser);
        return NoContent();
    }
}
