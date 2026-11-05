using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Piggy3.Api.Auth;
using Piggy3.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration["MONGO_CONNECTION_STRING"] ?? "mongodb://localhost:27017"));
builder.Services.AddSingleton<IMongoContext>(sp =>
    new MongoContext(
        sp.GetRequiredService<IMongoClient>(),
        builder.Configuration["MONGO_DATABASE_NAME"] ?? "piggy3"));

builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IMonthlyCostRepository, MonthlyCostRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ITotpSecretRepository, TotpSecretRepository>();

builder.Services.AddSingleton<AccountTypes>();
builder.Services.AddSingleton<PasswordVerifier>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<TotpService>();

var jwtSecret = builder.Configuration["AUTH_JWT_SECRET"] ?? string.Empty;
if (jwtSecret.Length < 32)
    throw new InvalidOperationException("AUTH_JWT_SECRET must be at least 32 characters.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "piggy3",
            ValidateAudience = true,
            ValidAudience = "piggy3",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
