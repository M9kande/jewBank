using GoonBank.Models;
using Microsoft.EntityFrameworkCore;

namespace GoonBank;

public class JewBankDbContext : DbContext
{
    public JewBankDbContext(DbContextOptions<JewBankDbContext> options) : base(options) {}
    
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<MoneyTransaction> Transactions => Set<MoneyTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BlihCode>  BlihCodes => Set<BlihCode>();
    public DbSet<BlihPayment> BlihPayments => Set<BlihPayment>();
    public DbSet<BlihProfile> BlihProfiles => Set<BlihProfile>();
}