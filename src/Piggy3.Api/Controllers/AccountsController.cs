using Microsoft.AspNetCore.Mvc;
using Piggy3.Api.Auth;
using Piggy3.Api.Data;
using Piggy3.Shared;

namespace Piggy3.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly IAccountRepository _accounts;
    private readonly ITransactionRepository _transactions;
    private readonly AccountTypes _types;

    public AccountsController(
        IAccountRepository accounts,
        ITransactionRepository transactions,
        AccountTypes types)
    {
        _accounts = accounts;
        _transactions = transactions;
        _types = types;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountRow>>> Get()
    {
        var accounts = await _accounts.GetAllAsync();
        var transactions = await _transactions.GetAllAsync();

        var sums = transactions
            .GroupBy(t => t.AccountId)
            .ToDictionary(g => g.Key, g => Money.Round(g.Sum(t => t.Amount)));

        var rows = accounts
            .Select(a => new AccountRow(
                a.Id,
                a.Name,
                a.Type,
                sums.GetValueOrDefault(a.Id, 0m)))
            .OrderBy(a => TypeOrder(a.Type, _types.Types))
            .ThenBy(a => a.Name, StringComparer.Ordinal)
            .ToList();

        return Ok(rows);
    }

    private static int TypeOrder(string type, IReadOnlyList<string> configured)
    {
        for (var i = 0; i < configured.Count; i++)
        {
            if (configured[i] == type)
                return i;
        }

        return int.MaxValue;
    }
}
