using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using GoonBank.DTOs;
using GoonBank.Enums;
using GoonBank.Models;
using GoonBank.Services;

namespace GoonBank.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly JewBankDbContext _dbContext;
    private readonly IAuditService _auditService;

    public AccountController(JewBankDbContext context, IAuditService auditService)
    {
        _dbContext = context;
        _auditService = auditService;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AccountResponse>> GetAccount(string id)
    {
        var account = await _dbContext.Accounts.FindAsync(id);

        if (account == null)
        {
            return NotFound(new { message = $"Account with ID {id} not found." });
        }

        return Ok(AccountResponse.FromAccount(account));
    }

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> CreateAccount([FromBody] CreateAccountRequest request)
    {
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return BadRequest(new { message = "Date of birth cannot be in the future." });
        }

        var emailExists = await _dbContext.Accounts.AnyAsync(a => a.Email.ToLower() == request.Email.ToLower());
        if (emailExists)
        {
            return Conflict(new { message = "An account with this email address already exists." });
        }

        string accountNo;
        do
        {
            accountNo = GenerateAccountNumber();
        } while (await _dbContext.Accounts.AnyAsync(a => a.AccountNo == accountNo));

        var now = DateTime.UtcNow;

        var account = new Account
        {
            AccountId = Guid.NewGuid().ToString(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            DateOfBirth = request.DateOfBirth,
            PasswordHash = HashPassword(request.PasswordHash),
            AccountNo = accountNo,
            Currency = request.Currency.ToUpperInvariant(),
            AccountType = request.AccountType,
            Status = AccountStatus.Active,
            OpenedAt = now,
            CreatedAt = now,
            ClosedAt = null,
            Balance = 0
        };

        await _dbContext.Accounts.AddAsync(account);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            AuditEntityType.Account,
            account.AccountId,
            AuditAction.Create,
            ActorType.Customer,
            customerId: account.AccountId,
            details: new { account.AccountNo, account.Currency, account.AccountType });

        return CreatedAtAction(nameof(GetAccount), new { id = account.AccountId }, AccountResponse.FromAccount(account));
    }

    [HttpGet("{id}/transactions")]
    public async Task<IActionResult> GetTransactions(string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var account = await _dbContext.Accounts.FindAsync(id);
        if (account == null)
        {
            return NotFound(new { message = $"Account with ID {id} not found." });
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var transactions = await _dbContext.Transactions
            .Where(t => t.SourceAccountId == id || t.TargetAccountId == id)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(transactions);
    }

    [HttpPatch("{id}/status")]
    public async Task<ActionResult> ChangeAccountStatus(string id, [FromBody] AccountStatus newStatus)
    {
        var account = await _dbContext.Accounts.FindAsync(id);

        if (account == null)
        {
            return NotFound(new { message = $"Account with ID {id} not found." });
        }

        // Prevent reopening a closed account
        if (account.Status == AccountStatus.Closed)
        {
            return BadRequest(new { message = "Cannot change the status of a closed account." });
        }

        // Validate closing conditions
        if (newStatus == AccountStatus.Closed)
        {
            if (account.Balance > 0)
            {
                return BadRequest(new { message = "Cannot close an account with an active balance. Please withdraw or transfer remaining funds first." });
            }
            account.ClosedAt = DateTime.UtcNow;
        }

        var oldStatus = account.Status;
        account.Status = newStatus;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            AuditEntityType.Account,
            account.AccountId,
            AuditAction.Update,
            ActorType.Customer,
            customerId: account.AccountId,
            details: new { OldStatus = oldStatus, NewStatus = newStatus });

        return Ok(new { message = $"Account status changed to {account.Status}." });
    }

    private static string HashPassword(string password)
    {
        if (password.Length == 64 && password.All(c => "0123456789abcdefABCDEF".Contains(c)))
        {
            return password.ToLowerInvariant();
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // Generates a random numeric account number (26 digits).
    private static string GenerateAccountNumber(int length = 26)
    {
        var digits = new char[length];
        for (int i = 0; i < length; i++)
        {
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        return new string(digits);
    }
}
