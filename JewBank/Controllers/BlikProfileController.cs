using JewBank.DTOs;
using JewBank.Enums;
using JewBank.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JewBank.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlikProfileController : ControllerBase
{
    private readonly JewBankDbContext _dbContext;

    public BlikProfileController(JewBankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<ActionResult<BlikProfile>> CreateProfile([FromBody] CreateBlikProfileRequest request)
    {
        var accountExists = await _dbContext.Accounts.AnyAsync(a => a.AccountId == request.AccountId);
        if (!accountExists)
        {
            return NotFound(new { message = $"account with ID {request.AccountId} not found." });
        }

        var profileExists = await _dbContext.BlikProfiles
            .AnyAsync(profile => profile.AccountId == request.AccountId);
        if (profileExists)
        {
            return Conflict(new { message = "account already has a BLIK profile." });
        }

        var profile = new BlikProfile
        {
            BlikProfileId = Guid.NewGuid().ToString(),
            AccountId = request.AccountId,
            PhoneHash = request.PhoneHash,
            Status = BlikProfileStatus.Active,
            RegisteredAt = DateTime.UtcNow
        };

        _dbContext.BlikProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        return Ok(profile);
    }

    [HttpGet("{accountId}")]
    public async Task<ActionResult<BlikProfile>> GetProfile(string accountId)
    {
        var profile = await _dbContext.BlikProfiles
            .FirstOrDefaultAsync(candidate => candidate.AccountId == accountId);

        return profile == null ? NotFound() : Ok(profile);
    }
}