using GoonBank;
using GoonBank.DTOs;
using GoonBank.Enums;
using GoonBank.Extensions;
using GoonBank.Models;
using GoonBank.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoonBank.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionController : ControllerBase
{
    private readonly JewBankDbContext _dbContext;
    private readonly IAuditService _auditService;

    public TransactionController(JewBankDbContext context, IAuditService auditService)
    {
        _dbContext = context;
        _auditService = auditService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTransaction(string id)
    {
        var transaction = await _dbContext.Transactions.FindAsync(id);

        if (transaction == null)
        {
            return NotFound(new { message = "Transaction not found." });
        }

        var currentUserId = User.GetAccountId();
        if (currentUserId != transaction.SourceAccountId && currentUserId != transaction.TargetAccountId)
        {
            return Forbid();
        }

        return Ok(transaction);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
    {
        var currentUserId = User.GetAccountId();
        if (currentUserId != request.SourceAccountId)
        {
            return Forbid();
        }

        var sourceAccount = await _dbContext.Accounts.FindAsync(request.SourceAccountId);
        if (sourceAccount == null)
        {
            return BadRequest(new { message = "Source account not found." });
        }

        if (sourceAccount.Status != AccountStatus.Active)
        {
            return BadRequest(new { message = $"Source account is not active (status: {sourceAccount.Status})." });
        }

        // Validate target account for transfers
        if (request.TransactionType == TransactionType.Transfer)
        {
            var targetAccount = await _dbContext.Accounts.FindAsync(request.TargetAccountId);
            if (targetAccount == null)
            {
                return BadRequest(new { message = "Target account not found." });
            }

            if (targetAccount.Status != AccountStatus.Active)
            {
                return BadRequest(new { message = $"Target account is not active (status: {targetAccount.Status})." });
            }

            if (!string.Equals(sourceAccount.Currency, targetAccount.Currency, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = $"Cross-currency transfers are not supported. Source: {sourceAccount.Currency}, Target: {targetAccount.Currency}." });
            }
        }

        var transaction = new MoneyTransaction
        {
            TransactionId = Guid.NewGuid().ToString(),
            Amount = request.Amount,
            SourceAccountId = request.SourceAccountId,
            TargetAccountId = request.TargetAccountId,
            Type = request.TransactionType,
            Title = request.Description,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.Transactions.AddAsync(transaction);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            AuditEntityType.Transaction,
            transaction.TransactionId,
            AuditAction.Create,
            ActorType.Customer,
            customerId: sourceAccount.AccountId,
            details: new { transaction.Amount, transaction.Type, transaction.SourceAccountId, transaction.TargetAccountId });

        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.TransactionId }, transaction);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult> ChangeTransactionStatus(string id, [FromBody] TransactionStatus status)
    {
        var transaction = await _dbContext.Transactions.FindAsync(id);

        if (transaction == null)
        {
            return NotFound(new { message = $"Transaction with ID {id} not found." });
        }

        var currentUserId = User.GetAccountId();
        if (currentUserId != transaction.SourceAccountId)
        {
            return Forbid();
        }

        if (transaction.Status != TransactionStatus.Pending)
        {
            return BadRequest(new { message = $"Cannot modify status of a transaction that is already {transaction.Status}." });
        }

        if (status == TransactionStatus.Completed)
        {
            return BadRequest(new { message = "Transactions can only be completed via the execution endpoint." });
        }

        transaction.Status = status;
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            AuditEntityType.Transaction,
            transaction.TransactionId,
            AuditAction.Update,
            details: new { NewStatus = status });

        return Ok(new { message = $"Transaction status changed to {transaction.Status}." });
    }

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> CancelTransaction(string id)
    {
        var transaction = await _dbContext.Transactions.FindAsync(id);

        if (transaction == null)
        {
            return NotFound(new { message = $"Transaction with ID {id} not found." });
        }

        var currentUserId = User.GetAccountId();
        if (currentUserId != transaction.SourceAccountId)
        {
            return Forbid();
        }

        if (transaction.Status != TransactionStatus.Pending)
        {
            return BadRequest(new { message = $"Cannot cancel a transaction that is already {transaction.Status}." });
        }

        transaction.Status = TransactionStatus.Failed;
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            AuditEntityType.Transaction,
            transaction.TransactionId,
            AuditAction.Update,
            details: new { Action = "Cancelled" });

        return Ok(new { message = "Transaction cancelled successfully." });
    }

    [HttpPut("{id}/execute")]
    public async Task<IActionResult> ExecuteTransaction(string id)
    {
        var transaction = await _dbContext.Transactions.FindAsync(id);

        if (transaction == null)
        {
            return NotFound(new { message = $"Transaction with ID {id} not found." });
        }

        var currentUserId = User.GetAccountId();
        if (currentUserId != transaction.SourceAccountId)
        {
            return Forbid();
        }

        await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            if (transaction.Type == TransactionType.Blih)
            {
                return BadRequest(new { message = "BLIH transactions must be executed through the BLIH payment flow." });
            }

            // Double execution check
            if (transaction.Status != TransactionStatus.Pending)
            {
                return Conflict(new { message = $"Transaction is already {transaction.Status}." });
            }

            var sourceAccount = await _dbContext.Accounts.FindAsync(transaction.SourceAccountId);

            if (sourceAccount == null)
            {
                transaction.Status = TransactionStatus.Failed;
                await _dbContext.SaveChangesAsync();
                await dbTransaction.CommitAsync();
                return BadRequest(new { message = "Source account not found." });
            }

            if (sourceAccount.Status != AccountStatus.Active)
            {
                transaction.Status = TransactionStatus.Failed;
                await _dbContext.SaveChangesAsync();
                await dbTransaction.CommitAsync();
                return BadRequest(new { message = $"Source account is not active (status: {sourceAccount.Status})." });
            }

            switch (transaction.Type)
            {
                case TransactionType.Transfer:
                {
                    if (sourceAccount.Balance < transaction.Amount)
                    {
                        transaction.Status = TransactionStatus.Failed;
                        await _dbContext.SaveChangesAsync();
                        await dbTransaction.CommitAsync();
                        return BadRequest(new { message = "Transaction amount exceeds account balance." });
                    }

                    var targetAccount = await _dbContext.Accounts.FindAsync(transaction.TargetAccountId);
                    if (targetAccount == null)
                    {
                        transaction.Status = TransactionStatus.Failed;
                        await _dbContext.SaveChangesAsync();
                        await dbTransaction.CommitAsync();
                        return BadRequest(new { message = "Target account not found." });
                    }

                    if (targetAccount.Status != AccountStatus.Active)
                    {
                        transaction.Status = TransactionStatus.Failed;
                        await _dbContext.SaveChangesAsync();
                        await dbTransaction.CommitAsync();
                        return BadRequest(new { message = $"Target account is not active (status: {targetAccount.Status})." });
                    }

                    if (!string.Equals(sourceAccount.Currency, targetAccount.Currency, StringComparison.OrdinalIgnoreCase))
                    {
                        transaction.Status = TransactionStatus.Failed;
                        await _dbContext.SaveChangesAsync();
                        await dbTransaction.CommitAsync();
                        return BadRequest(new { message = $"Currency mismatch between accounts ({sourceAccount.Currency} vs {targetAccount.Currency})." });
                    }

                    sourceAccount.Balance -= transaction.Amount;
                    targetAccount.Balance += transaction.Amount;
                    break;
                }
                case TransactionType.Deposit:
                {
                    sourceAccount.Balance += transaction.Amount;
                    break;
                }
                case TransactionType.Withdrawal:
                {
                    if (sourceAccount.Balance < transaction.Amount)
                    {
                        transaction.Status = TransactionStatus.Failed;
                        await _dbContext.SaveChangesAsync();
                        await dbTransaction.CommitAsync();
                        return BadRequest(new { message = "Transaction amount exceeds account balance." });
                    }

                    sourceAccount.Balance -= transaction.Amount;
                    break;
                }
                default:
                    transaction.Status = TransactionStatus.Failed;
                    await _dbContext.SaveChangesAsync();
                    await dbTransaction.CommitAsync();
                    return BadRequest(new { message = "Transaction type not supported." });
            }

            transaction.Status = TransactionStatus.Completed;
            await _dbContext.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            await _auditService.LogAsync(
                AuditEntityType.Transaction,
                transaction.TransactionId,
                AuditAction.Update,
                customerId: sourceAccount.AccountId,
                details: new { Status = transaction.Status, transaction.Amount, transaction.Type });

            return Ok(transaction);
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }
}
