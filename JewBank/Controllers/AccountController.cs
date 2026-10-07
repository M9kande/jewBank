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
    private const string DefaultBankCode = "12345678"; // replace with your bank's real 8-digit code

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
            Iban = GenerateIbanNumber(accountNo),
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
    private static string GenerateAccountNumber(int length = 16)
    {
        var digits = new char[length];
        for (int i = 0; i < length; i++)
        {
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        return new string(digits);
    }

    // Builds a valid IBAN: country code + 2 check digits + bank code + account number.
    private static string GenerateIbanNumber(
        string accountNumber,
        string bankCode = DefaultBankCode,
        string countryCode = DefaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(accountNumber) || !accountNumber.All(char.IsDigit))
            throw new ArgumentException("Account number must contain digits only.", nameof(accountNumber));

        var bban = bankCode + accountNumber;

        // ISO 13616: append country code + "00", convert letters to numbers, check digits = 98 - (value mod 97)
        var checkDigits = 98 - Mod97(bban + countryCode + "00");

        return $"{countryCode}{checkDigits:D2}{bban}";
    }

    // Validates an IBAN checksum (useful later for incoming transfers).
    private static bool IsValidIban(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return false;

        iban = iban.Replace(" ", "").ToUpperInvariant();
        if (iban.Length < 15 || iban.Length > 34 || !iban.All(char.IsLetterOrDigit)) return false;

        // Move first 4 chars to the end
        var rearranged = iban[4..] + iban[..4];
        return Mod97(rearranged) == 1;
    }

    // Computes mod 97 piece by piece, so no BigInteger is needed.
    // Letters map to 10..35 (A=10 ... Z=35), which takes two digits.
    private static int Mod97(string input)
    {
        int remainder = 0;
        foreach (var ch in input)
        {
            if (char.IsDigit(ch))
            {
                remainder = (remainder * 10 + (ch - '0')) % 97;
            }
            else
            {
                int value = char.ToUpperInvariant(ch) - 'A' + 10;
                remainder = (remainder * 100 + value) % 97;
            }
        }

        return remainder;
    }
}