using Microsoft.AspNetCore.Mvc;

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
    
    
    
    



}