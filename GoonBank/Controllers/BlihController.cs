using System.Security.Cryptography;
using System.Text;
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
public class BlihController : ControllerBase
{
    private readonly JewBankDbContext _dbContext;
    private readonly IAuditService _auditService;

    public BlihController(JewBankDbContext context, IAuditService auditService)
    {
        _dbContext = context;
        _auditService = auditService;
    }

    // Customer
    [HttpPost("code")]
    public async Task<ActionResult<BlihCodeResponse>> GenerateCode([FromBody] GenerateBlihCodeRequest request)
    {
        var currentUserId = User.GetAccountId();
        if (currentUserId != request.AccountId)
        {
            return Forbid();
        }

        var profile = await _dbContext.BlihProfiles.FirstOrDefaultAsync(p => p.AccountId == request.AccountId);

        if (profile == null || profile.Status != BlihProfileStatus.Active)
        {
            return BadRequest(new { message = "Customer has not enabled BLIH or profile is not active." });
        }

        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.AccountId == request.AccountId);
        if (account == null || account.Status != AccountStatus.Active)
        {
            return BadRequest(new { message = "No active bank account associated with this customer." });
        }

        string rawCode;
        string codeHash;
        bool isCodeTaken;

        do
        {
            rawCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            codeHash = HashBlihCode(rawCode);

            isCodeTaken = await _dbContext.BlihCodes.AnyAsync(c =>
                c.CodeHash == codeHash && c.Status == BlihCodeStatus.Active && c.ExpiresAt > DateTime.UtcNow);
        } while (isCodeTaken);

        var activeCodes = await _dbContext.BlihCodes
            .Where(b => b.BlihProfileId == profile.BlihProfileId && b.Status == BlihCodeStatus.Active)
            .ToListAsync();

        foreach (var code in activeCodes)
        {
            code.Status = BlihCodeStatus.Expired;
        }

        // Standard BLIK/BLIH codes are valid for 2 minutes
        var expiresAt = DateTime.UtcNow.AddMinutes(2);

        var blihCodeEntity = new BlihCode
        {
            BlihCodeId = Guid.NewGuid().ToString(),
            BlihProfileId = profile.BlihProfileId,
            CodeHash = codeHash,
            ExpiresAt = expiresAt,
            Status = BlihCodeStatus.Active
        };

        _dbContext.BlihCodes.Add(blihCodeEntity);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            AuditEntityType.BlihTransaction,
            blihCodeEntity.BlihCodeId,
            AuditAction.Create,
            customerId: account.AccountId,
            details: new { ExpiresAt = expiresAt });

        return Ok(new BlihCodeResponse
        {
            Code = rawCode,
            ExpiresAt = expiresAt
        });
    }

    // Merchant (or unauthenticated merchant terminal)
    [HttpPost("payment/initiate")]
    [AllowAnonymous]
    public async Task<ActionResult> InitiatePayment([FromBody] InitiateBlihPaymentRequest request)
    {
        var codeHash = HashBlihCode(request.BlihCode);

        var code = await _dbContext.BlihCodes.FirstOrDefaultAsync(c =>
            c.CodeHash == codeHash && c.Status == BlihCodeStatus.Active && c.ExpiresAt > DateTime.UtcNow);

        if (code == null)
        {
            return BadRequest(new { message = "Invalid or expired BLIH code." });
        }

        var profile = await _dbContext.BlihProfiles.FindAsync(code.BlihProfileId);
        if (profile == null || profile.Status != BlihProfileStatus.Active)
        {
            return BadRequest(new { message = "BLIH profile is not active." });
        }

        var payerAccount = await _dbContext.Accounts.FirstOrDefaultAsync(a =>
            a.AccountId == profile.AccountId && a.Status == AccountStatus.Active);

        if (payerAccount == null)
        {
            return BadRequest(new { message = "No active bank account associated with this BLIH profile." });
        }

        // Consume code immediately so it cannot be double-used
        code.Status = BlihCodeStatus.Used;
        code.UsedAt = DateTime.UtcNow;

        var payment = new BlihPayment
        {
            BlihPaymentId = Guid.NewGuid().ToString(),
            TransactionId = Guid.NewGuid().ToString(),
            BlihCodeId = code.BlihCodeId,
            PayerAccountId = payerAccount.AccountId,
            MerchantId = request.MerchantId,
            Amount = request.Amount,
            Status = BlihPaymentStatus.Pending,
            InitiatedAt = DateTime.UtcNow,
        };

        _dbContext.BlihPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            AuditEntityType.BlihTransaction,
            payment.BlihPaymentId,
            AuditAction.Create,
            customerId: payerAccount.AccountId,
            details: new { payment.MerchantId, payment.Amount });

        return Ok(new
        {
            paymentId = payment.BlihPaymentId,
            message = "Waiting for customer authorization."
        });
    }

    // Customer
    [HttpGet("payment/pending/{accountId}")]
    public async Task<IActionResult> GetPendingPayment(string accountId)
    {
        var currentUserId = User.GetAccountId();
        if (currentUserId != accountId)
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;

        var pendingPayment = await _dbContext.BlihPayments
            .Where(p => p.PayerAccountId == accountId && p.Status == BlihPaymentStatus.Pending)
            .OrderByDescending(p => p.InitiatedAt)
            .FirstOrDefaultAsync();

        if (pendingPayment == null)
        {
            return NotFound(new { message = "No pending payments found." });
        }

        // Pending payments expire after 2 minutes
        if (pendingPayment.InitiatedAt.AddMinutes(2) < now)
        {
            pendingPayment.Status = BlihPaymentStatus.Failed;
            await _dbContext.SaveChangesAsync();
            return NotFound(new { message = "Pending payment has expired." });
        }

        return Ok(new
        {
            pendingPayment.BlihPaymentId,
            pendingPayment.Amount,
            pendingPayment.MerchantId,
            pendingPayment.InitiatedAt
        });
    }

    // Customer
    [HttpPost("payment/{paymentId}/confirm")]
    public async Task<IActionResult> ConfirmPayment(string paymentId)
    {
        var payment = await _dbContext.BlihPayments.FindAsync(paymentId);
        if (payment == null || payment.Status != BlihPaymentStatus.Pending)
        {
            return BadRequest(new { message = "Payment not found or already processed." });
        }

        var currentUserId = User.GetAccountId();
        if (currentUserId != payment.PayerAccountId)
        {
            return Forbid();
        }

        await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            // Expiry check (2 minutes)
            if (payment.InitiatedAt.AddMinutes(2) < DateTime.UtcNow)
            {
                payment.Status = BlihPaymentStatus.Failed;
                await _dbContext.SaveChangesAsync();
                await dbTransaction.CommitAsync();
                return BadRequest(new { message = "Payment authorization has timed out." });
            }

            var account = await _dbContext.Accounts.FindAsync(payment.PayerAccountId);
            if (account == null || account.Status != AccountStatus.Active)
            {
                payment.Status = BlihPaymentStatus.Failed;
                await _dbContext.SaveChangesAsync();
                await dbTransaction.CommitAsync();
                return BadRequest(new { message = "Account is not active." });
            }

            if (account.Balance < payment.Amount)
            {
                payment.Status = BlihPaymentStatus.Failed;
                await _dbContext.SaveChangesAsync();
                await dbTransaction.CommitAsync();
                return BadRequest(new { message = "Insufficient funds." });
            }

            account.Balance -= payment.Amount;
            payment.Status = BlihPaymentStatus.Approved;
            payment.CompletedAt = DateTime.UtcNow;

            // Credit merchant if they hold an account in the bank
            var merchantAccount = await _dbContext.Accounts.FindAsync(payment.MerchantId);
            if (merchantAccount != null && merchantAccount.Status == AccountStatus.Active)
            {
                merchantAccount.Balance += payment.Amount;
            }

            // Create a formal MoneyTransaction so it appears in the customer's transaction history
            var moneyTransaction = new MoneyTransaction
            {
                TransactionId = payment.TransactionId,
                SourceAccountId = account.AccountId,
                TargetAccountId = payment.MerchantId,
                Amount = payment.Amount,
                Type = TransactionType.Blih,
                Status = TransactionStatus.Completed,
                Title = $"BLIH Payment - {payment.MerchantId}",
                CreatedAt = DateTime.UtcNow
            };
            await _dbContext.Transactions.AddAsync(moneyTransaction);

            await _dbContext.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            await _auditService.LogAsync(
                AuditEntityType.BlihTransaction,
                payment.BlihPaymentId,
                AuditAction.Update,
                customerId: account.AccountId,
                details: new { Status = "Approved", payment.Amount, payment.MerchantId });

            return Ok(new { message = "Payment authorized successfully." });
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }

    // Customer
    [HttpPost("payment/{paymentId}/reject")]
    public async Task<IActionResult> RejectPayment(string paymentId)
    {
        var payment = await _dbContext.BlihPayments.FindAsync(paymentId);
        if (payment == null || payment.Status != BlihPaymentStatus.Pending)
        {
            return BadRequest(new { message = "Payment not found or already processed." });
        }

        var currentUserId = User.GetAccountId();
        if (currentUserId != payment.PayerAccountId)
        {
            return Forbid();
        }

        payment.Status = BlihPaymentStatus.Failed;
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            AuditEntityType.BlihTransaction,
            payment.BlihPaymentId,
            AuditAction.Update,
            customerId: payment.PayerAccountId,
            details: new { Status = "Rejected" });

        return Ok(new { message = "Payment rejected by user." });
    }

    // Merchant 
    [HttpGet("payment/{paymentId}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckPaymentStatus(string paymentId)
    {
        var payment = await _dbContext.BlihPayments.FindAsync(paymentId);
        if (payment == null)
        {
            return NotFound();
        }

        return Ok(new { status = payment.Status.ToString() });
    }

    private static string HashBlihCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
}
