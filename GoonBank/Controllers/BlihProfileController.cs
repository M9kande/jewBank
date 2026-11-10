using GoonBank.DTOs;
using GoonBank.Enums;
using GoonBank.Models;
using GoonBank.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoonBank.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlihProfileController : ControllerBase
{
    private readonly JewBankDbContext _dbContext;
    private readonly IAuditService _auditService;

    public BlihProfileController(JewBankDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    [HttpPost]
    public async Task<ActionResult<BlihProfile>> CreateProfile([FromBody] CreateBlihProfileRequest request)
    {
        var account = await _dbContext.Accounts.FindAsync(request.AccountId);
        if (account == null)
        {
            return NotFound(new { message = $"Account with ID {request.AccountId} not found." });
        }

        if (account.Status != AccountStatus.Active)
        {
            return BadRequest(new { message = $"Cannot register BLIH profile for inactive account (status: {account.Status})." });
        }

        var profileExists = await _dbContext.BlihProfiles
            .AnyAsync(profile => profile.AccountId == request.AccountId);
        if (profileExists)
        {
            return Conflict(new { message = "Account already has a BLIH profile." });
        }

        var phoneInUse = await _dbContext.BlihProfiles
            .AnyAsync(p => p.PhoneHash == request.PhoneHash && p.Status == BlihProfileStatus.Active);
        if (phoneInUse)
        {
            return Conflict(new { message = "Phone number is already linked to an active BLIH profile." });
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

        await _auditService.LogAsync(
            AuditEntityType.Customer,
            profile.BlihProfileId,
            AuditAction.Create,
            customerId: account.AccountId,
            details: new { profile.AccountId, profile.Status });

        return Ok(profile);
    }

    [HttpGet("{accountId}")]
    public async Task<ActionResult<BlihProfile>> GetProfile(string accountId)
    {
        var profile = await _dbContext.BlihProfiles
            .FirstOrDefaultAsync(candidate => candidate.AccountId == accountId);

        return profile == null ? NotFound(new { message = $"BLIH profile for account {accountId} not found." }) : Ok(profile);
    }
}
