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
    
    // [HttpPost("code")]
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
    

    private static string HashBlikCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
    
    
    
    
    
    



}