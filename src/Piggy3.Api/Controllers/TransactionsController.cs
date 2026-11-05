using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Piggy3.Api.Data;
using Piggy3.Api.Models;
using Piggy3.Shared;

namespace Piggy3.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionRepository _transactions;
    private readonly IAccountRepository _accounts;

    public TransactionsController(
        ITransactionRepository transactions,
        IAccountRepository accounts)
    {
        _transactions = transactions;
        _accounts = accounts;
    }

    [HttpGet]
    public async Task<ActionResult<TransactionListResponse>> Get()
    {
        var accounts = (await _accounts.GetAllAsync(includeDeleted: true))
            .ToDictionary(a => a.Id, a => a.Name);

        var rows = (await _transactions.GetAllAsync())
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => ToRow(t, accounts.GetValueOrDefault(t.AccountId, string.Empty)))
            .ToList();

        var total = Money.Round(rows.Sum(r => r.Amount));
        return Ok(new TransactionListResponse(rows, total));
    }

    [HttpPost]
    public async Task<ActionResult<TransactionRow>> Create([FromBody] TransactionRequest request)
    {
        var account = await _accounts.GetByIdAsync(request.AccountId);
        if (account is null)
            return NotFound();

        var transaction = new Transaction { AccountId = account.Id };
        var error = ApplyRequest(transaction, request);
        if (error is not null)
            return BadRequest(error);

        await _transactions.CreateAsync(transaction);
        return Ok(ToRow(transaction, account.Name));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TransactionRow>> Update(
        string id,
        [FromBody] TransactionRequest request)
    {
        var transaction = await _transactions.GetByIdAsync(id);
        if (transaction is null)
            return NotFound();

        var account = await _accounts.GetByIdAsync(request.AccountId);
        if (account is null)
            return NotFound();

        var error = ApplyRequest(transaction, request);
        if (error is not null)
            return BadRequest(error);

        transaction.AccountId = account.Id;
        await _transactions.UpdateAsync(transaction);
        return Ok(ToRow(transaction, account.Name));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var transaction = await _transactions.GetByIdAsync(id);
        if (transaction is null)
            return NotFound();

        await _transactions.DeleteAsync(id);
        return NoContent();
    }

    private static string? ApplyRequest(Transaction transaction, TransactionRequest request)
    {
        if (!DateOnly.TryParseExact(
                request.Date,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            return "Date is invalid.";

        if (!Money.TryParse(request.Amount, out var amount))
            return "Amount is invalid.";

        transaction.Date = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        transaction.Amount = amount;
        return null;
    }

    private static TransactionRow ToRow(Transaction transaction, string accountName) =>
        new(
            transaction.Id,
            transaction.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            transaction.Amount,
            transaction.AccountId,
            accountName);
}
