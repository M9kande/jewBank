using System.Security.Cryptography;
using System.Text;
using JewBank.DTOs;
using JewBank.Enums;
using JewBank.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JewBank.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlikController : ControllerBase
{
    private readonly JewBankDbContext _dbContext;

    public BlikController(JewBankDbContext context)
    {
        _dbContext = context;
    }

    // Customer
    [HttpPost("code")]
    public async Task<ActionResult<BlikCodeResponse>> GenerateCode([FromBody] GenerateBlikCodeRequest request)
    {
        var profile = await _dbContext.BlikProfiles.FirstOrDefaultAsync(p => p.CustomerId == request.CustomerId);

        if (profile == null || profile.Status != BlikProfileStatus.Active)
        {
            return BadRequest(new { message = "Customer has not enabled BLIK." });
        }

        var accountExists = await _dbContext.Accounts.AnyAsync(account =>
            account.AccountId == request.AccountId &&
            account.CustomerId == request.CustomerId &&
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
            rawCode = Random.Shared.Next(100000, 999999).ToString();
            codeHash = HashBlikCode(rawCode);


            isCodeTaken = await _dbContext.BlikCodes.AnyAsync(c =>
                (c.CodeHash == codeHash && c.Status == BlikCodeStatus.Active && c.ExpiresAt > DateTime.UtcNow));
        } while (isCodeTaken);


        var activeCodes = await _dbContext.BlikCodes
            .Where(b => b.BlikProfileId == profile.BlikProfileId && b.Status == BlikCodeStatus.Active)
            .ToListAsync();


        foreach (var code in activeCodes)
        {
            code.Status = BlikCodeStatus.Expired;
        }

        var expiresAt = DateTime.UtcNow.AddHours(2);

        var blikCodeEntity = new BlikCode
        {
            BlikCodeId = Guid.NewGuid().ToString(),
            BlikProfileId = profile.BlikProfileId,
            CodeHash = codeHash,
            ExpiresAt = expiresAt,
            Status = BlikCodeStatus.Active
        };

        _dbContext.BlikCodes.Add(blikCodeEntity);
        await _dbContext.SaveChangesAsync();

        return Ok(new BlikCodeResponse
        {
            Code = rawCode,
            ExpiresAt = expiresAt
        });
    }

    // Merchant
    [HttpPost("payment/initiate")]
    public async Task<ActionResult<InitiateBlikPaymentRequest>> InitiatePayment(
        [FromBody] InitiateBlikPaymentRequest request)
    {
        var codeHash = HashBlikCode(request.BlikCode);


        var code = await _dbContext.BlikCodes.FirstOrDefaultAsync(c =>
            c.CodeHash == codeHash && c.Status == BlikCodeStatus.Active && c.ExpiresAt > DateTime.UtcNow);

        if (code == null)
        {
            return BadRequest(new { message = "Invalid or expired BLIK code" });
        }

        var profile = await _dbContext.BlikProfiles.FindAsync(code.BlikProfileId);
        if (profile == null || profile.Status != BlikProfileStatus.Active)
        {
            return BadRequest(new { message = "BLIK profile is not active." });
        }

        var payerAccount = await _dbContext.Accounts.FirstOrDefaultAsync(a =>
            a.CustomerId == profile.CustomerId && a.Status == AccountStatus.Active);

        if (payerAccount == null)
        {
            return BadRequest(new { message = "No active bank account associated with this BLIK profile" });
        }

        var payment = new BlikPayment
        {
            BlikPaymentId = Guid.NewGuid().ToString(),
            TransactionId = Guid.NewGuid().ToString(),
            BlikCodeId = code.BlikCodeId,
            PayerAccountId = payerAccount.AccountId,
            MerchantId = request.MerchantId,
            Amount = request.Amount,
            Status = BlikPaymentStatus.Pending,
            InitiatedAt = DateTime.UtcNow,
        };

        _dbContext.BlikPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            paymentId = payment.BlikPaymentId, message = "Waiting for cusomer auhtorization. "
        });
    }

    // Customer
    [HttpGet("payment/pending/{customerId}")]
    public async Task<IActionResult> GetPendingPayment(String customerId)
    {
        var pendingPayment = await _dbContext.BlikPayments.Join(_dbContext.Accounts,
                payment => payment.PayerAccountId,
                account => account.AccountId,
                (payment, account) => new { payment, account })
            .Where(x => x.account.CustomerId == customerId && x.payment.Status == BlikPaymentStatus.Pending)
            .Select(x => new
            {
                x.payment.BlikPaymentId,
                x.payment.Amount,
                x.payment.MerchantId,
                x.payment.InitiatedAt,
            })
            .FirstOrDefaultAsync();

        if (pendingPayment == null)
        {
            return NotFound(new { message = "No peding payments found" });
        }

        return Ok(pendingPayment);
    }


    // Customer
    [HttpPost("payment/{paymentId}/confirm")]
    public async Task<IActionResult> ConfirmPayment(String paymentId)
    {
        var payment = await _dbContext.BlikPayments.FindAsync(paymentId);
        if (payment == null || payment.Status != BlikPaymentStatus.Pending)
        {
            return BadRequest(new { message = "Payment not found or already processed" });
        }

        ;

        var account = await _dbContext.Accounts.FindAsync(payment.PayerAccountId);
        if (account == null || account.Balance < payment.Amount)
        {
            payment.Status = BlikPaymentStatus.Failed;
            await _dbContext.SaveChangesAsync();
            return BadRequest(new { message = "Insufficient Funds." });
        }

        account.Balance -= payment.Amount;
        payment.Status = BlikPaymentStatus.Approved;
        payment.CompletedAt = DateTime.UtcNow;

        var code = await _dbContext.BlikCodes.FindAsync(payment.BlikCodeId);
        if (code != null)
        {
            code.Status = BlikCodeStatus.Used;
            code.UsedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Payment authorized successfully" });
    }

    // Customer
    [HttpPost("payment/{paymentID}/reject")]
    public async Task<IActionResult> RejectPayment(String paymentId)
    {
        var payment = await _dbContext.BlikPayments.FindAsync(paymentId);
        if (payment == null || payment.Status != BlikPaymentStatus.Pending)
        {
            return BadRequest(new { message = "Payment not found or already processed" });
        }

        payment.Status = BlikPaymentStatus.Failed;
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Payment rejected by User" });
    }


    // Merchant 
    [HttpGet("payment/{paymentId}/status")]
    public async Task<IActionResult> CheckPaymentStatus(string paymentId)
    {
        var payment = await _dbContext.BlikPayments.FindAsync(paymentId);
        if (payment == null)
        {
            return NotFound();
        }

        return Ok(new { status = payment.Status.ToString() });
    }


    // Helper method
    private static string HashBlikCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
}