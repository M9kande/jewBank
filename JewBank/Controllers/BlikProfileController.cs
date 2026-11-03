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
        var customerExists = await _dbContext.Customers
            .AnyAsync(customer => customer.CustomerId == request.CustomerId);
        if (!customerExists)
        {
            return NotFound(new { message = $"Customer with ID {request.CustomerId} not found." });
        }

        var profileExists = await _dbContext.BlikProfiles
            .AnyAsync(profile => profile.CustomerId == request.CustomerId);
        if (profileExists)
        {
            return Conflict(new { message = "Customer already has a BLIK profile." });
        }

        var profile = new BlikProfile
        {
            BlikProfileId = Guid.NewGuid().ToString(),
            CustomerId = request.CustomerId,
            PhoneHash = request.PhoneHash,
            Status = BlikProfileStatus.Active,
            RegisteredAt = DateTime.UtcNow
        };

        _dbContext.BlikProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        return Ok(profile);
    }

    [HttpGet("{customerId}")]
    public async Task<ActionResult<BlikProfile>> GetProfile(string customerId)
    {
        var profile = await _dbContext.BlikProfiles
            .FirstOrDefaultAsync(candidate => candidate.CustomerId == customerId);

        return profile == null ? NotFound() : Ok(profile);
    }
}