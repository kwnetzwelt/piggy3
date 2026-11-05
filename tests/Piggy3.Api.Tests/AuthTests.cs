using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Piggy3.Api.Auth;
using Piggy3.Api.Controllers;
using Piggy3.Shared;

namespace Piggy3.Api.Tests;

public class AuthTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static IConfiguration ValidConfig() => Config(
        ("AUTH_USERNAME", "kai"),
        ("AUTH_PASSWORD", "secret"),
        ("AUTH_JWT_SECRET", "0123456789abcdef0123456789abcdef"));

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
    public async Task Login_ReturnsToken_ForValidCredentials()
    {
        var controller = new AuthController(
            new PasswordVerifier(ValidConfig()),
            new JwtTokenService(ValidConfig()));

        var result = await controller.Login(new LoginRequest("kai", "secret"));

        var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForBadCredentials()
    {
        var controller = new AuthController(
            new PasswordVerifier(ValidConfig()),
            new JwtTokenService(ValidConfig()));

        var result = await controller.Login(new LoginRequest("kai", "nope"));

        Assert.IsType<Microsoft.AspNetCore.Mvc.UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ApiRoutes_RequireAuthentication_ButLoginIsOpen()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("AUTH_USERNAME", "kai")
                .UseSetting("AUTH_PASSWORD", "secret")
                .UseSetting("AUTH_JWT_SECRET", "0123456789abcdef0123456789abcdef")
                .UseSetting("MONGO_CONNECTION_STRING", "mongodb://localhost:27017"));
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/account-types")).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("kai", "secret"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Token;

        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var types = await client.GetAsync("/api/account-types");
        Assert.Equal(HttpStatusCode.OK, types.StatusCode);
        Assert.Contains("Ausgabe", await types.Content.ReadAsStringAsync());
    }
}
