using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piggy3.Frontend.Services;
using Piggy3.Shared;

namespace Piggy3.Frontend.Pages;

public record AccountGroup(string Type, IReadOnlyList<AccountRow> Rows);
public record TypeTotal(string Type, decimal Total);
public record AccountOption(string Id, string Name);

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public string ActiveTab { get; private set; } = "konten";
    public IReadOnlyList<AccountGroup> AccountGroups { get; private set; } = [];
    public IReadOnlyList<MonthlyCostRow> MonthlyCosts { get; private set; } = [];
    public IReadOnlyList<MonthlyCostRow> ActiveMonthlyCosts { get; private set; } = [];
    public IReadOnlyList<TypeTotal> MonthlyTypeTotals { get; private set; } = [];
    public decimal MonthlyGrandTotal { get; private set; }
    public bool ShowDeleted { get; private set; }
    public IReadOnlyList<string> AccountTypes { get; private set; } = [];
    public IReadOnlyList<AccountOption> AccountOptions { get; private set; } = [];
    public IReadOnlyList<TransactionRow> Transactions { get; private set; } = [];
    public decimal TransactionTotal { get; private set; }
    public string DefaultDate { get; private set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public string? ErrorMessage => TempData["Error"] as string;

    public async Task<IActionResult> OnGetAsync(string? tab, bool showDeleted = false)
    {
        ActiveTab = tab is "kosten" or "buchungen" ? tab : "konten";
        ShowDeleted = showDeleted;

        try
        {
            AccountTypes = await _api.GetAccountTypesAsync();

            if (ActiveTab == "konten")
                await LoadAccountsAsync();
            else if (ActiveTab == "kosten")
                await LoadMonthlyCostsAsync(showDeleted);
            else
                await LoadTransactionsAsync();
        }
        catch (ApiException)
        {
            SetError("Die Daten konnten nicht geladen werden.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAddMonthlyCostAsync(string name, string type, string value)
    {
        try
        {
            await _api.CreateMonthlyCostAsync(new MonthlyCostRequest(name, type, value));
        }
        catch (ApiException)
        {
            SetError("Der Eintrag konnte nicht gespeichert werden.");
        }

        return RedirectToPage("/Index", new { tab = "kosten" });
    }

    public async Task<IActionResult> OnPostUpdateMonthlyCostAsync(
        string accountId, string name, string type, string value)
    {
        try
        {
            await _api.UpdateMonthlyCostAsync(accountId, new MonthlyCostRequest(name, type, value));
        }
        catch (ApiException)
        {
            SetError("Der Eintrag konnte nicht gespeichert werden.");
        }

        return RedirectToPage("/Index", new { tab = "kosten" });
    }

    public async Task<IActionResult> OnPostDeleteMonthlyCostAsync(string accountId)
    {
        try
        {
            await _api.DeleteMonthlyCostAsync(accountId);
        }
        catch (ApiException)
        {
            SetError("Der Eintrag konnte nicht gelöscht werden.");
        }

        return RedirectToPage("/Index", new { tab = "kosten", showDeleted = true });
    }

    public async Task<IActionResult> OnPostRestoreMonthlyCostAsync(string accountId)
    {
        try
        {
            await _api.RestoreMonthlyCostAsync(accountId);
        }
        catch (ApiException)
        {
            SetError("Der Eintrag konnte nicht wiederhergestellt werden.");
        }

        return RedirectToPage("/Index", new { tab = "kosten", showDeleted = true });
    }

    public async Task<IActionResult> OnPostAddTransactionAsync(
        string date, string value, string accountId)
    {
        try
        {
            await _api.CreateTransactionAsync(new TransactionRequest(date, value, accountId));
        }
        catch (ApiException)
        {
            SetError("Die Buchung konnte nicht gespeichert werden.");
        }

        return RedirectToPage("/Index", new { tab = "buchungen" });
    }

    public async Task<IActionResult> OnPostUpdateTransactionAsync(
        string id, string date, string value, string accountId)
    {
        try
        {
            await _api.UpdateTransactionAsync(id, new TransactionRequest(date, value, accountId));
        }
        catch (ApiException)
        {
            SetError("Die Buchung konnte nicht gespeichert werden.");
        }

        return RedirectToPage("/Index", new { tab = "buchungen" });
    }

    public async Task<IActionResult> OnPostDeleteTransactionAsync(string id)
    {
        try
        {
            await _api.DeleteTransactionAsync(id);
        }
        catch (ApiException)
        {
            SetError("Die Buchung konnte nicht gelöscht werden.");
        }

        return RedirectToPage("/Index", new { tab = "buchungen" });
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }

    private async Task LoadAccountsAsync()
    {
        var accounts = await _api.GetAccountsAsync();
        var types = AccountTypes
            .Concat(accounts.Select(a => a.Type).Where(t => !AccountTypes.Contains(t)))
            .Distinct()
            .ToList();

        AccountGroups = types
            .Select(type => new AccountGroup(
                type,
                accounts.Where(a => a.Type == type).ToList()))
            .Where(group => group.Rows.Count > 0)
            .ToList();
    }

    private async Task LoadMonthlyCostsAsync(bool includeDeleted)
    {
        MonthlyCosts = await _api.GetMonthlyCostsAsync(includeDeleted);
        ActiveMonthlyCosts = MonthlyCosts.Where(c => !c.IsDeleted).ToList();
        AccountOptions = (await _api.GetAccountsAsync())
            .Select(a => new AccountOption(a.Id, a.Name))
            .ToList();

        var types = AccountTypes
            .Concat(ActiveMonthlyCosts.Select(c => c.Type).Where(t => !AccountTypes.Contains(t)))
            .Distinct()
            .ToList();

        MonthlyTypeTotals = types
            .Select(type => new TypeTotal(
                type,
                ActiveMonthlyCosts.Where(c => c.Type == type).Sum(c => c.Value)))
            .ToList();

        MonthlyGrandTotal = ActiveMonthlyCosts.Sum(c => c.Value);
    }

    private async Task LoadTransactionsAsync()
    {
        var transactions = await _api.GetTransactionsAsync();
        Transactions = transactions.Items;
        TransactionTotal = transactions.Total;
        DefaultDate = DateTime.Today.ToString("yyyy-MM-dd");
        AccountOptions = (await _api.GetAccountsAsync())
            .Select(a => new AccountOption(a.Id, a.Name))
            .ToList();
        ActiveMonthlyCosts = (await _api.GetMonthlyCostsAsync(false))
            .Where(c => !c.IsDeleted)
            .ToList();
    }

    private void SetError(string message) => TempData["Error"] = message;
}
