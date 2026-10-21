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
    
    [HttpPost("code")]
    public async Task<ActionResult<BlikCodeResponse>> GenerateCode([FromBody] GenerateBlikCodeRequest request)
    {
        var profile = await _dbContext.BlikProfiles.FirstOrDefaultAsync(p => p.CustomerId == request.CustomerId);
        
        if (profile == null)
        {
            return BadRequest(new { message = "Customer has not enabled blik" });            
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
        _dbContext.SaveChangesAsync();

        return Ok(new BlikCodeResponse
        {
            Code = rawCode,
            ExpiresAt = expiresAt
        });

    }
    
    [HttpPost("payment/initiate")]
    public async Task<ActionResult<InitiateBlikPaymentRequest>> InitiatePayment(
        [FromBody] InitiateBlikPaymentRequest request)
    {
        var codeHash = HashBlikCode(request.BlikCode);
        
        
        var code = await _dbContext.BlikCodes.FirstOrDefaultAsync(c => c.CodeHash == codeHash &&  c.Status == BlikCodeStatus.Active && c.ExpiresAt > DateTime.UtcNow);

        if (code == null)
        {
            return BadRequest(new { message = "Invalid or expired BLIK code" });
        }

        var profile = await _dbContext.Accounts.FindAsync(code.BlikProfileId);
        var payerAccount = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.CustomerId == profile!.CustomerId && a.Status == AccountStatus.Active);

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
        _dbContext.SaveChangesAsync();

        return Ok(new
        {
            paymentId = payment.BlikPaymentId, message = "Waiting for cusomer auhtorization. "
        });

    } 
    
    [HttpGet("payment/pending/{customerId}")]
    public async Task<IActionResult> GetPendingPayment(String customerId)
    {
        var pendingPayment = await _dbContext.BlikPayments.Join(_dbContext.Accounts, 
            payment => payment.BlikPaymentId,
            account => account.AccountId,
            (payment, account) => new {payment, account })
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

    [HttpPost("payment/{paymentId}/confirm")]
    public async Task<ActionResult<ConfirmBlikPaymentRequest>> ConfirmPayment(String paymentId,
        [FromBody] ConfirmBlikPaymentRequest request)
    {
        var payment = await _dbContext.BlikPayments.FindAsync(paymentId);
        if (payment == null || payment.Status != BlikPaymentStatus.Pending)
        {
            return BadRequest(new { message = "Payment not found or already processed" });
        };

        if (!request.Accept)
        {
            payment.Status = BlikPaymentStatus.Failed;
            _dbContext.SaveChangesAsync();
            return Ok(new { message = "Payment declined" });
        }

        var account = await _dbContext.Accounts.FindAsync(payment.PayerAccountId);
        if (account == null || account.Balance < payment.Amount)
        {
            payment.Status = BlikPaymentStatus.Failed;
            await _dbContext.SaveChangesAsync();
            return BadRequest(new { message = "Insufficient Funds." });
        }
        
        


    }
    
    

    private static string HashBlikCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
    
    
    
    
    
    



}