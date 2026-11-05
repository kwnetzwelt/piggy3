using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using OtpNet;
using Piggy3.Api.Auth;
using Piggy3.Api.Controllers;
using Piggy3.Api.Data;
using Piggy3.Api.Models;
using Piggy3.Shared;

namespace Piggy3.Api.Tests;

public class AuthTests
{
    private const string JwtSecret = "0123456789abcdef0123456789abcdef";

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static IConfiguration ValidConfig() => Config(
        ("AUTH_USERNAME", "kai"),
        ("AUTH_PASSWORD", "secret"),
        ("AUTH_JWT_SECRET", JwtSecret));

    private static AuthController Controller(FakeTotpSecretRepository repo)
    {
        var controller = new AuthController(
            new PasswordVerifier(ValidConfig()),
            new JwtTokenService(ValidConfig()),
            repo,
            new TotpService());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "kai")], "Test"))
            }
        };

        return controller;
    }

    private static string NewSecret() => new TotpService().GenerateSecret();

    private static string CurrentCode(string secret) =>
        new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

    [Fact]
    public void PasswordVerifier_AcceptsExactCredentials()
    {
        Assert.True(new PasswordVerifier(ValidConfig()).IsValid("kai", "secret"));
    }

    [Theory]
    [InlineData("kai", "wrong")]
    [InlineData("other", "secret")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void PasswordVerifier_RejectsWrongCredentials(string? user, string? password)
    {
        Assert.False(new PasswordVerifier(ValidConfig()).IsValid(user, password));
    }

    [Fact]
    public void JwtTokenService_CreatesTokenWithSubjectAndSevenDayExpiry()
    {
        var service = new JwtTokenService(ValidConfig());
        var token = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken("kai"));

        Assert.Equal("kai", token.Subject);
        Assert.InRange((token.ValidTo - DateTime.UtcNow).TotalDays, 6.9, 7.1);
    }

    [Fact]
    public void JwtTokenService_ChallengeToken_HasDifferentAudience()
    {
        var service = new JwtTokenService(ValidConfig());
        var token = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateChallengeToken("kai"));

        Assert.Contains("piggy3-2fa", token.Audiences);
        Assert.True(service.TryValidateChallengeToken(service.CreateChallengeToken("kai"), out var user));
        Assert.Equal("kai", user);
        Assert.False(service.TryValidateChallengeToken(service.CreateToken("kai"), out _));
    }

    [Fact]
    public void TotpService_VerifiesGeneratedCode()
    {
        var service = new TotpService();
        var secret = service.GenerateSecret();

        Assert.True(service.Verify(secret, CurrentCode(secret)));
        Assert.False(service.Verify(secret, "000000"));
    }

    [Fact]
    public async Task Login_ReturnsToken_ForValidCredentials()
    {
        var result = await Controller(new FakeTotpSecretRepository())
            .Login(new LoginRequest("kai", "secret"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.False(response.TwoFactorRequired);
        Assert.Equal("kai", response.Username);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForBadCredentials()
    {
        var result = await Controller(new FakeTotpSecretRepository())
            .Login(new LoginRequest("kai", "nope"));

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Login_ReturnsChallenge_WhenTwoFactorIsEnabled()
    {
        var repo = new FakeTotpSecretRepository();
        var secret = NewSecret();
        repo.Secrets.Add(new TotpSecret { UserId = "kai", Secret = secret });

        var result = await Controller(repo).Login(new LoginRequest("kai", "secret"));

        var response = Assert.IsType<LoginResponse>(((OkObjectResult)result).Value);
        Assert.True(response.TwoFactorRequired);
        Assert.Null(response.Token);
        Assert.False(string.IsNullOrWhiteSpace(response.ChallengeToken));
    }

    [Fact]
    public async Task VerifyTwoFactor_ReturnsToken_ForValidCode()
    {
        var repo = new FakeTotpSecretRepository();
        var secret = NewSecret();
        repo.Secrets.Add(new TotpSecret { UserId = "kai", Secret = secret });
        var controller = Controller(repo);
        var challenge = new JwtTokenService(ValidConfig()).CreateChallengeToken("kai");

        var result = await controller.Verify(
            new TwoFactorVerifyRequest(challenge, CurrentCode(secret)));

        var response = Assert.IsType<LoginResponse>(((OkObjectResult)result).Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.Equal("kai", response.Username);
    }

    [Fact]
    public async Task VerifyTwoFactor_RejectsWrongCode()
    {
        var repo = new FakeTotpSecretRepository();
        var secret = NewSecret();
        repo.Secrets.Add(new TotpSecret { UserId = "kai", Secret = secret });
        var controller = Controller(repo);
        var challenge = new JwtTokenService(ValidConfig()).CreateChallengeToken("kai");
        var wrong = CurrentCode(secret) == "999999" ? "111111" : "999999";

        var result = await controller.Verify(new TwoFactorVerifyRequest(challenge, wrong));

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task VerifyTwoFactor_RejectsAccessTokenAsChallenge()
    {
        var repo = new FakeTotpSecretRepository();
        var secret = NewSecret();
        repo.Secrets.Add(new TotpSecret { UserId = "kai", Secret = secret });
        var controller = Controller(repo);
        var accessToken = new JwtTokenService(ValidConfig()).CreateToken("kai");

        var result = await controller.Verify(
            new TwoFactorVerifyRequest(accessToken, CurrentCode(secret)));

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task TwoFactorSetup_ReturnsSecretAndUri()
    {
        var result = Controller(new FakeTotpSecretRepository()).Setup();

        var setup = Assert.IsType<TwoFactorSetupResponse>(((OkObjectResult)result.Result!).Value);
        Assert.False(string.IsNullOrWhiteSpace(setup.Secret));
        Assert.StartsWith("otpauth://totp/Piggy3:kai", setup.OtpauthUri);
    }

    [Fact]
    public async Task EnableTwoFactor_StoresSecret_WhenCodeIsValid()
    {
        var repo = new FakeTotpSecretRepository();
        var secret = NewSecret();

        var result = await Controller(repo).Enable(
            new TwoFactorEnableRequest(secret, CurrentCode(secret)));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(secret, repo.Secrets.Single().Secret);
        Assert.Equal("kai", repo.Secrets.Single().UserId);
    }

    [Fact]
    public async Task EnableTwoFactor_RejectsWrongCode_AndStoresNothing()
    {
        var repo = new FakeTotpSecretRepository();
        var secret = NewSecret();
        var wrong = CurrentCode(secret) == "999999" ? "111111" : "999999";

        var result = await Controller(repo).Enable(new TwoFactorEnableRequest(secret, wrong));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(repo.Secrets);
    }

    [Fact]
    public async Task StatusAndDisable_ReflectStoredSecret()
    {
        var repo = new FakeTotpSecretRepository();
        var controller = Controller(repo);
        repo.Secrets.Add(new TotpSecret { UserId = "kai", Secret = NewSecret() });

        var status = await controller.Status();
        var enabled = Assert.IsType<TwoFactorStatusResponse>(
            ((OkObjectResult)status.Result!).Value);
        Assert.True(enabled.Enabled);

        var disable = await controller.Disable();
        Assert.IsType<NoContentResult>(disable);
        Assert.Empty(repo.Secrets);
    }

    [Fact]
    public async Task ApiRoutes_RequireAuthentication_ButLoginIsOpen()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("AUTH_USERNAME", "kai")
                .UseSetting("AUTH_PASSWORD", "secret")
                .UseSetting("AUTH_JWT_SECRET", JwtSecret)
                .UseSetting("MONGO_CONNECTION_STRING", "mongodb://localhost:27017"));
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/account-types")).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("kai", "secret"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Token!;

        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var types = await client.GetAsync("/api/account-types");
        Assert.Equal(HttpStatusCode.OK, types.StatusCode);
        Assert.Contains("Ausgabe", await types.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ChallengeToken_IsNotAcceptedAsApiBearerToken()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("AUTH_USERNAME", "kai")
                .UseSetting("AUTH_PASSWORD", "secret")
                .UseSetting("AUTH_JWT_SECRET", JwtSecret)
                .UseSetting("MONGO_CONNECTION_STRING", "mongodb://localhost:27017"));
        var client = factory.CreateClient();

        var challenge = new JwtTokenService(ValidConfig()).CreateChallengeToken("kai");
        client.DefaultRequestHeaders.Authorization = new("Bearer", challenge);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/account-types")).StatusCode);
    }
}
