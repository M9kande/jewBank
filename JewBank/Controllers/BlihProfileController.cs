using System;
using System.Threading.Tasks;
using JewBank.DTOs;
using JewBank.Enums;
using JewBank.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JewBank.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlihProfileController : ControllerBase
{
    private readonly JewBankDbContext _dbContext;

    public BlihProfileController(JewBankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<ActionResult<BlihProfile>> CreateProfile([FromBody] CreateBlihProfileRequest request)
    {
        var accountExists = await _dbContext.Accounts.AnyAsync(a => a.AccountId == request.AccountId);
        if (!accountExists)
        {
            return NotFound(new { message = $"account with ID {request.AccountId} not found." });
        }

        var profileExists = await _dbContext.BlihProfiles
            .AnyAsync(profile => profile.AccountId == request.AccountId);
        if (profileExists)
        {
            return Conflict(new { message = "account already has a BLIH profile." });
        }

        var profile = new BlihProfile
        {
            BlihProfileId = Guid.NewGuid().ToString(),
            AccountId = request.AccountId,
            PhoneHash = request.PhoneHash,
            Status = BlihProfileStatus.Active,
            RegisteredAt = DateTime.UtcNow
        };

        _dbContext.BlihProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        return Ok(profile);
    }

    [HttpGet("{accountId}")]
    public async Task<ActionResult<BlihProfile>> GetProfile(string accountId)
    {
        var profile = await _dbContext.BlihProfiles
            .FirstOrDefaultAsync(candidate => candidate.AccountId == accountId);

        return profile == null ? NotFound() : Ok(profile);
    }
}