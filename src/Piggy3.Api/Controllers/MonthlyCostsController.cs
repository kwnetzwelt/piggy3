using Microsoft.AspNetCore.Mvc;
using Piggy3.Api.Auth;
using Piggy3.Api.Data;
using Piggy3.Api.Models;
using Piggy3.Shared;

namespace Piggy3.Api.Controllers;

[ApiController]
[Route("api/monthly-costs")]
public class MonthlyCostsController : ControllerBase
{
    private readonly IAccountRepository _accounts;
    private readonly IMonthlyCostRepository _costs;
    private readonly AccountTypes _types;

    public MonthlyCostsController(
        IAccountRepository accounts,
        IMonthlyCostRepository costs,
        AccountTypes types)
    {
        _accounts = accounts;
        _costs = costs;
        _types = types;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MonthlyCostRow>>> Get(bool includeDeleted = false)
    {
        var accounts = await _accounts.GetAllAsync(includeDeleted);
        var costs = (await _costs.GetAllAsync()).ToDictionary(c => c.AccountId);

        var rows = accounts
            .Select(a => ToRow(a, costs.GetValueOrDefault(a.Id)?.Value ?? 0m))
            .OrderBy(r => TypeOrder(r.Type))
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ToList();

        return Ok(rows);
    }

    [HttpPost]
    public async Task<ActionResult<MonthlyCostRow>> Create([FromBody] MonthlyCostRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            return BadRequest("Name is required.");

        if (!Money.TryParse(request.Value, out var value))
            return BadRequest("Value is invalid.");

        if (!_types.Contains(request.Type))
            return BadRequest("Type is not configured.");

        var active = await _accounts.GetAllAsync();
        if (active.Any(a => a.Name == name))
            return BadRequest("An active account with this name already exists.");

        var account = new Account { Name = name, Type = request.Type };
        await _accounts.CreateAsync(account);

        var cost = new MonthlyCost { AccountId = account.Id, Value = value };
        await _costs.CreateAsync(cost);

        return Ok(ToRow(account, cost.Value));
    }

    [HttpPut("{accountId}")]
    public async Task<ActionResult<MonthlyCostRow>> Update(
        string accountId,
        [FromBody] MonthlyCostRequest request)
    {
        var account = await _accounts.GetByIdAsync(accountId);
        if (account is null || account.IsDeleted)
            return NotFound();

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            return BadRequest("Name is required.");

        if (!Money.TryParse(request.Value, out var value))
            return BadRequest("Value is invalid.");

        if (request.Type != account.Type && !_types.Contains(request.Type))
            return BadRequest("Type is not configured.");

        var active = await _accounts.GetAllAsync();
        if (active.Any(a => a.Id != accountId && a.Name == name))
            return BadRequest("An active account with this name already exists.");

        account.Name = name;
        account.Type = request.Type;
        await _accounts.UpdateAsync(account);

        var cost = await _costs.GetByAccountIdAsync(accountId);
        if (cost is null)
        {
            cost = new MonthlyCost { AccountId = accountId, Value = value };
            await _costs.CreateAsync(cost);
        }
        else
        {
            cost.Value = value;
            await _costs.UpdateAsync(cost);
        }

        return Ok(ToRow(account, cost.Value));
    }

    [HttpDelete("{accountId}")]
    public async Task<IActionResult> Delete(string accountId)
    {
        var account = await _accounts.GetByIdAsync(accountId);
        if (account is null || account.IsDeleted)
            return NotFound();

        account.IsDeleted = true;
        await _accounts.UpdateAsync(account);
        return NoContent();
    }

    [HttpPost("{accountId}/restore")]
    public async Task<IActionResult> Restore(string accountId)
    {
        var account = await _accounts.GetByIdAsync(accountId);
        if (account is null)
            return NotFound();

        if (!account.IsDeleted)
            return NoContent();

        var active = await _accounts.GetAllAsync();
        if (active.Any(a => a.Id != accountId && a.Name == account.Name))
            return BadRequest("An active account with this name already exists.");

        account.IsDeleted = false;
        await _accounts.UpdateAsync(account);
        return NoContent();
    }

    private static MonthlyCostRow ToRow(Account account, decimal value) =>
        new(account.Id, account.Name, account.Type, value, account.IsDeleted);

    private int TypeOrder(string type)
    {
        for (var i = 0; i < _types.Types.Count; i++)
        {
            if (_types.Types[i] == type)
                return i;
        }

        return int.MaxValue;
    }
}
