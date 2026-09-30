using JewBank.Models;

using Microsoft.EntityFrameworkCore;

namespace JewBank;

public class JewBankDbContext : DbContext
{
    public JewBankDbContext(DbContextOptions<JewBankDbContext> options) : base(options) {}
    
    
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();
    public DbSet<BlikCode>  BlikCodes => Set<BlikCode>();
    public DbSet<BlikPayment> BlikPayments => Set<BlikPayment>();
    public DbSet<BlikProfile> BlikProfiles => Set<BlikProfile>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    
}