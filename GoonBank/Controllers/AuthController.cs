using System.Security.Cryptography;
using System.Text;
using GoonBank.DTOs;
using GoonBank.Enums;
using GoonBank.Extensions;
using GoonBank.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoonBank.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JewBankDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _auditService;

    public AuthController(JewBankDbContext dbContext, ITokenService tokenService, IAuditService auditService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _auditService = auditService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail);

        if (account == null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var inputHash = HashPassword(request.Password);
        if (!string.Equals(account.PasswordHash, inputHash, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (account.Status != AccountStatus.Active)
        {
            return Unauthorized(new { message = $"Account is not active (status: {account.Status})." });
        }

        var (token, expiresAt) = _tokenService.GenerateToken(account);

        await _auditService.LogAsync(
            AuditEntityType.Session,
            account.AccountId,
            AuditAction.Create,
            ActorType.Customer,
            customerId: account.AccountId,
            details: new { Action = "UserLogin" });

        return Ok(new AuthResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            AccountId = account.AccountId,
            Email = account.Email,
            FirstName = account.FirstName,
            LastName = account.LastName,
            AccountNo = account.AccountNo,
            Currency = account.Currency,
            Balance = account.Balance,
            AccountType = account.AccountType
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AccountResponse>> GetCurrentUser()
    {
        var accountId = User.GetAccountId();
        if (string.IsNullOrEmpty(accountId))
        {
            return Unauthorized();
        }

        var account = await _dbContext.Accounts.FindAsync(accountId);
        if (account == null)
        {
            return NotFound(new { message = "Account not found." });
        }

        return Ok(AccountResponse.FromAccount(account));
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
}
