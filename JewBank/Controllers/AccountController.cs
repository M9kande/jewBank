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

    [HttpGet]
    
    //Return account info like balanc etc.
    public async Task<ActionResult<IEnumerable<Account>>> GetAccount()
    {
        var accounts = await _dbContext.Accounts.ToListAsync();
        return Ok(accounts);
    }
}