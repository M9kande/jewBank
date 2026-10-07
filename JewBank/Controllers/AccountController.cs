using Microsoft.AspNetCore.Mvc;
using JewBank;
using JewBank.Models;
using Microsoft.EntityFrameworkCore;

namespace JewBank.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly JewBankDbContext _dbContext;

    public AccountController(JewBankDbContext context)
    {
        _dbContext = context;   
    }

    [HttpGet("{id}")]
    
    //Return account info like balanc etc.
    public async Task<ActionResult<Account>> GetAccount(string id)
    {
        var account = await _dbContext.Accounts.FindAsync(id);

        if (account == null)
        {
            return NotFound(new { message = $"Account with ID {id} not found." });
        }
        return Ok(account);
    }
}