using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using JewBank.DTOs;
using JewBank.Enums;
using JewBank.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JewBank.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlihController : ControllerBase
{
    private readonly JewBankDbContext _dbContext;

    public BlihController(JewBankDbContext context)
    {
        _dbContext = context;
    }

    // Customer
    [HttpPost("code")]
    public async Task<ActionResult<BlihCodeResponse>> GenerateCode([FromBody] GenerateBlihCodeRequest request)
    {
        var profile = await _dbContext.BlihProfiles.FirstOrDefaultAsync(p => p.AccountId == request.AccountId);

        if (profile == null || profile.Status != BlihProfileStatus.Active)
        {
            return BadRequest(new { message = "Customer has not enabled BLIH." });
        }

        var accountExists = await _dbContext.Accounts.AnyAsync(account =>
            account.AccountId == request.AccountId &&
            account.Status == AccountStatus.Active);
        if (!accountExists)
        {
            return BadRequest(new { message = "No active bank account associated with this customer." });
        }

        String rawCode;
        String codeHash;
        bool isCodeTaken;

        do
        {
            rawCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            codeHash = HashBlihCode(rawCode);


            isCodeTaken = await _dbContext.BlihCodes.AnyAsync(c =>
                (c.CodeHash == codeHash && c.Status == BlihCodeStatus.Active && c.ExpiresAt > DateTime.UtcNow));
        } while (isCodeTaken);


        var activeCodes = await _dbContext.BlihCodes
            .Where(b => b.BlihProfileId == profile.BlihProfileId && b.Status == BlihCodeStatus.Active)
            .ToListAsync();


        foreach (var code in activeCodes)
        {
            code.Status = BlihCodeStatus.Expired;
        }

        var expiresAt = DateTime.UtcNow.AddHours(2);

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

        return Ok(new BlihCodeResponse
        {
            Code = rawCode,
            ExpiresAt = expiresAt
        });
    }

    // Merchant
    [HttpPost("payment/initiate")]
    public async Task<IActionResult> InitiatePayment(
        [FromBody] InitiateBlihPaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Payment amount must be greater than zero." });
        }

        if (string.IsNullOrWhiteSpace(request.MerchantId))
        {
            return BadRequest(new { message = "Merchant ID is required." });
        }

        var codeHash = HashBlihCode(request.BlihCode);


        var code = await _dbContext.BlihCodes.FirstOrDefaultAsync(c =>
            c.CodeHash == codeHash && c.Status == BlihCodeStatus.Active && c.ExpiresAt > DateTime.UtcNow);

        if (code == null)
        {
            return BadRequest(new { message = "Invalid or expired BLIH code" });
        }

        var hasPendingPayment = await _dbContext.BlihPayments.AnyAsync(p =>
            p.BlihCodeId == code.BlihCodeId && p.Status == BlihPaymentStatus.Pending);
        if (hasPendingPayment)
        {
            return Conflict(new { message = "A payment is already pending for this BLIH code." });
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
            return BadRequest(new { message = "No active bank account associated with this BLIH profile" });
        }

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

        return Ok(new
        {
            paymentId = payment.BlihPaymentId, message = "Waiting for cusomer auhtorization. "
        });
    }

    // Customer
    [HttpGet("payment/pending/{accountId}")]
    public async Task<IActionResult> GetPendingPayment(string accountId)
    {
        var pendingPayment = await _dbContext.BlihPayments.Join(_dbContext.Accounts,
                payment => payment.PayerAccountId,
                account => account.AccountId,
                (payment, account) => new { payment, account })
            .Where(x => x.account.AccountId == accountId && x.payment.Status == BlihPaymentStatus.Pending)
            .Select(x => new
            {
                x.payment.BlihPaymentId,
                x.payment.Amount,
                x.payment.MerchantId,
                x.payment.InitiatedAt,
            })
            .FirstOrDefaultAsync();

        if (pendingPayment == null)
        {
            return NotFound(new { message = "No pending payments found." });
        }

        return Ok(pendingPayment);
    }


    // Customer
    [HttpPost("payment/{paymentId}/confirm")]
    public async Task<IActionResult> ConfirmPayment(String paymentId)
    {
        var payment = await _dbContext.BlihPayments.FindAsync(paymentId);
        if (payment == null || payment.Status != BlihPaymentStatus.Pending)
        {
            return BadRequest(new { message = "Payment not found or already processed" });
        }

        var account = await _dbContext.Accounts.FindAsync(payment.PayerAccountId);
        if (account == null || account.Status != AccountStatus.Active || payment.Amount <= 0)
        {
            payment.Status = BlihPaymentStatus.Failed;
            await _dbContext.SaveChangesAsync();
            return BadRequest(new { message = "The payer account is unavailable or the payment amount is invalid." });
        }

        if (account.Balance < payment.Amount)
        {
            payment.Status = BlihPaymentStatus.Failed;
            await _dbContext.SaveChangesAsync();
            return BadRequest(new { message = "Insufficient funds." });
        }

        account.Balance -= payment.Amount;
        payment.Status = BlihPaymentStatus.Approved;
        payment.CompletedAt = DateTime.UtcNow;

        var code = await _dbContext.BlihCodes.FindAsync(payment.BlihCodeId);
        if (code != null)
        {
            code.Status = BlihCodeStatus.Used;
            code.UsedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Payment authorized successfully" });
    }

    // Customer
    [HttpPost("payment/{paymentId}/reject")]
    public async Task<IActionResult> RejectPayment(string paymentId)
    {
        var payment = await _dbContext.BlihPayments.FindAsync(paymentId);
        if (payment == null || payment.Status != BlihPaymentStatus.Pending)
        {
            return BadRequest(new { message = "Payment not found or already processed" });
        }

        payment.Status = BlihPaymentStatus.Failed;
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Payment rejected by User" });
    }


    // Merchant 
    [HttpGet("payment/{paymentId}/status")]
    public async Task<IActionResult> CheckPaymentStatus(string paymentId)
    {
        var payment = await _dbContext.BlihPayments.FindAsync(paymentId);
        if (payment == null)
        {
            return NotFound();
        }

        return Ok(new { status = payment.Status.ToString() });
    }


    // Helper method
    private static string HashBlihCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
}