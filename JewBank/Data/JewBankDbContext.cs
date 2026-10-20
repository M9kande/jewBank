using JewBank.Models;

using Microsoft.EntityFrameworkCore;

namespace JewBank;

public class JewBankDbContext : DbContext
{
    public JewBankDbContext(DbContextOptions<JewBankDbContext> options) : base(options) {}
    
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<MoneyTransaction> Transactions => Set<MoneyTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BlikCode>  BlikCodes => Set<BlikCode>();
    public DbSet<BlikPayment> BlikPayments => Set<BlikPayment>();
    public DbSet<BlikProfile> BlikProfiles => Set<BlikProfile>();
}