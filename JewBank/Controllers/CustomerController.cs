using JewBank.DTOs;
using JewBank.Enums;
using JewBank.Models;
using Microsoft.AspNetCore.Mvc;

namespace JewBank.Controllers;

[ApiController]
[Route("api/[controller]")]

public class CustomerController : Controller
{
    private readonly JewBankDbContext _dbContext;

    public CustomerController(JewBankDbContext context)
    {
        _dbContext = context;
    }

    [HttpGet("{id}")]
    
    //Return Customer info like balanc etc.
    public async Task<ActionResult<Customer>> GetCustomer(string id)
    {
        var customer = await _dbContext.Customers.FindAsync(id);

        if (customer == null)
        {
            return NotFound(new { message = $"Customer with ID {id} not found." });
        }
        return Ok(customer);
    }

    [HttpPost]
    public async Task<ActionResult<Customer>> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        var customer = new Customer()
        {
            CustomerId = Guid.NewGuid().ToString(),
            PasswordHash = request.PasswordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            DateOfBirth = request.DateOfBirth,
            Status = CustomerStatus.Active,
            CreatedAt = DateTime.Now
        };
        
        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, customer);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult> ChangeCustomerStatus(string id)
    {
        var customer = await _dbContext.Customers.FindAsync(id);

        if (customer == null)
        {
            return NotFound(new {message = $"Customer with ID {id} not found."});
        }

        if (customer.Status == CustomerStatus.Active)
        {
            customer.Status = CustomerStatus.Blocked;
        }
        else
        {
            customer.Status = CustomerStatus.Active;
        }
        
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = $"Account status changed to {customer.Status}." });
    }
    
    
}