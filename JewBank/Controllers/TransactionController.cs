using System.Transactions;
using JewBank.DTOs;
using JewBank.Enums;
using JewBank.Models;
using Microsoft.AspNetCore.Mvc;
using TransactionStatus = JewBank.Enums.TransactionStatus;

namespace JewBank.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionController : ControllerBase
{
    private readonly JewBankDbContext _dbContext;

    public TransactionController(JewBankDbContext context)
    {
        _dbContext = context;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTransaction(string id)
    {
        var transaction = await _dbContext.Transactions.FindAsync(id);

        if (transaction == null)
        {
            return NotFound(new {message = "Transaction not found."});
        }
        
        return Ok(transaction);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
    {
        var transaction = new MoneyTransaction()
        {
            TransactionId = Guid.NewGuid().ToString(),
            Amount = request.Amount,
            SourceAccountId = request.SourceAccountId,
            TargetAccountId = request.TargetAccountId,
            Type = request.TransactionType,
            Description = request.Description,
            Status = TransactionStatus.Pending
        };

        await _dbContext.Transactions.AddAsync(transaction);
        await _dbContext.SaveChangesAsync();
        return Ok(transaction);
    }
    
    [HttpPatch("{id}")]
    public async Task<ActionResult> ChangeTransactionStatus(string id, TransactionStatus status)
    {
        var transaction = await _dbContext.Transactions.FindAsync(id);

        if (transaction == null)
        {
            return NotFound(new {message = $"Transaction with ID {id} not found."});
        }
        
        transaction.Status = status;
        
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = $"Account status changed to {transaction.Status}." });
    }
    
    [HttpPut("{id}/execute")]
    public async Task<IActionResult> ExecuteTransaction(string id)
    {
        var transaction = await _dbContext.Transactions.FindAsync(id);
        
        if (transaction == null)
        {
            return NotFound(new {message = $"Transaction with ID {id} not found."});
        }

        if (transaction.Type == TransactionType.Blik)
        {
            return BadRequest(new {message = "Transaction type not supported."});
        }
        
        //Double execution check
        if (transaction.Status != TransactionStatus.Pending)
            return Conflict(new { message = $"Transaction is already {transaction.Status}." });
        
        var sourceAccount = await _dbContext.Accounts.FindAsync(transaction.SourceAccountId);

        if (sourceAccount == null)
        {
            transaction.Status = TransactionStatus.Failed;
            await _dbContext.SaveChangesAsync();
            return BadRequest(new {message = "Account not found."});
        }
        
        
        //Too little balance check
        if (sourceAccount.Balance < transaction.Amount)
        {
            transaction.Status = TransactionStatus.Failed;
            await _dbContext.SaveChangesAsync();
            return BadRequest(new {message = "Transaction amount exceeds account balance."});
        }
        
        
        //Transaction execution
        var targetAccount = await _dbContext.Accounts.FindAsync(transaction.TargetAccountId);
        
        if (targetAccount == null)
        {
            transaction.Status = TransactionStatus.Failed;
            return BadRequest(new  {message = "Target account not found."});
        }
        
        switch (transaction.Type)
        {
            case TransactionType.Transfer:
                sourceAccount.Balance -= transaction.Amount;
                targetAccount.Balance += transaction.Amount;
                break;
            case TransactionType.Deposit:
                sourceAccount.Balance += transaction.Amount;
                break;
            case TransactionType.Withdrawal:
                sourceAccount.Balance -= transaction.Amount;
                break;
            default:
                transaction.Status = TransactionStatus.Failed;
                return BadRequest(new {message = "Transaction type not supported."});
        }

        transaction.Status = TransactionStatus.Completed;
        await _dbContext.SaveChangesAsync();
        return Ok(transaction);
        
    }
    
}