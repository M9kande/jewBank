using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using JewBank.DTOs;
using JewBank.Enums;
using JewBank.Models;

namespace JewBank.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly JewBankDbContext _dbContext;
    private const string DefaultCountryCode = "PL";
    public AccountController(JewBankDbContext context)
    {
        _dbContext = context;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Account>> GetAccount(string id)
    {
        var account = await _dbContext.Accounts.FindAsync(id);

        if (account == null)
        {
            return NotFound(new { message = $"Account with ID {id} not found." });
        }

        return Ok(account);
    }

    [HttpPost]
    public async Task<ActionResult<Account>> CreateAccount([FromBody] CreateAccountRequest request)
    {
        var customerExists = await _dbContext.Customers
            .AnyAsync(c => c.CustomerId == request.CustomerId);

        if (!customerExists)
        {
            return NotFound(new { message = $"Customer with ID {request.CustomerId} not found." });
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
            CustomerId = request.CustomerId,
            AccountNo = accountNo,
            Currency = request.Currency,
            AccountType = request.AccountType,
            Status = AccountStatus.Active,
            OpenedAt = now,
            CreatedAt = now,
            ClosedAt = null,
            Balance = 0
        };

        await _dbContext.Accounts.AddAsync(account);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAccount), new { id = account.AccountId }, account);
    }
    
    public async Task<ActionResult> ChangeAccountStatus(string id)
    {
        var account = await _dbContext.Accounts.FindAsync(id);

        if (account == null)
        {
            return NotFound(new {message = $"Customer with ID {id} not found."});
        }

        if (account.Status == AccountStatus.Closed)
        {
            account.Status = AccountStatus.Active;
        }
        else
        {
            account.Status = AccountStatus.Active;
        }
        
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = $"Account status changed to {account.Status}." });
    }
    

    // Generates a random numeric account number (16 digits by default).
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