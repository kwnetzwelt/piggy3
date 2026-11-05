using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Piggy3.Shared;

namespace Piggy3.Frontend.Services;

public class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContext;

    public ApiClient(HttpClient http, IHttpContextAccessor httpContext)
    {
        _http = http;
        _httpContext = httpContext;
    }

    private string? Token =>
        _httpContext.HttpContext?.User.FindFirst("api_token")?.Value;

    public async Task<LoginResponse?> LoginAsync(string username, string password)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/auth/login", new LoginRequest(username, password), Json);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new ApiException(response.StatusCode, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
    }

    public async Task<TwoFactorStatusResponse> GetTwoFactorStatusAsync() =>
        await GetAsync<TwoFactorStatusResponse>("api/auth/2fa");

    public async Task<TwoFactorSetupResponse> SetupTwoFactorAsync() =>
        await SendAsync<TwoFactorSetupResponse>(HttpMethod.Post, "api/auth/2fa/setup");

    public async Task EnableTwoFactorAsync(string secret, string code) =>
        await SendAsync(HttpMethod.Post, "api/auth/2fa/enable",
            new TwoFactorEnableRequest(secret, code));

    public async Task DisableTwoFactorAsync() =>
        await SendAsync(HttpMethod.Delete, "api/auth/2fa");

    public async Task<LoginResponse?> VerifyTwoFactorAsync(string challengeToken, string code)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/auth/2fa/verify", new TwoFactorVerifyRequest(challengeToken, code), Json);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new ApiException(response.StatusCode, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
    }

    public async Task<List<AccountRow>> GetAccountsAsync() =>
        await GetAsync<List<AccountRow>>("api/accounts");

    public async Task<List<string>> GetAccountTypesAsync() =>
        await GetAsync<List<string>>("api/account-types");

    public async Task<List<MonthlyCostRow>> GetMonthlyCostsAsync(bool includeDeleted) =>
        await GetAsync<List<MonthlyCostRow>>($"api/monthly-costs?includeDeleted={includeDeleted.ToString().ToLowerInvariant()}");

    public async Task<MonthlyCostRow> CreateMonthlyCostAsync(MonthlyCostRequest request) =>
        await SendAsync<MonthlyCostRow>(HttpMethod.Post, "api/monthly-costs", request);

    public async Task<MonthlyCostRow> UpdateMonthlyCostAsync(string accountId, MonthlyCostRequest request) =>
        await SendAsync<MonthlyCostRow>(HttpMethod.Put, $"api/monthly-costs/{accountId}", request);

    public async Task DeleteMonthlyCostAsync(string accountId) =>
        await SendAsync(HttpMethod.Delete, $"api/monthly-costs/{accountId}");

    public async Task RestoreMonthlyCostAsync(string accountId) =>
        await SendAsync(HttpMethod.Post, $"api/monthly-costs/{accountId}/restore");

    public async Task<TransactionListResponse> GetTransactionsAsync() =>
        await GetAsync<TransactionListResponse>("api/transactions");

    public async Task<TransactionRow> CreateTransactionAsync(TransactionRequest request) =>
        await SendAsync<TransactionRow>(HttpMethod.Post, "api/transactions", request);

    public async Task<TransactionRow> UpdateTransactionAsync(string id, TransactionRequest request) =>
        await SendAsync<TransactionRow>(HttpMethod.Put, $"api/transactions/{id}", request);

    public async Task DeleteTransactionAsync(string id) =>
        await SendAsync(HttpMethod.Delete, $"api/transactions/{id}");

    private async Task<T> GetAsync<T>(string url)
    {
        using var response = await SendAsync(HttpMethod.Get, url);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var response = await SendAsync(method, url, body);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);

        if (Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(response.StatusCode, await response.Content.ReadAsStringAsync());

        return response;
    }
}
