using GoonBank.Enums;
using GoonBank.Models;
using System.Text.Json;

namespace GoonBank.Services;

public interface IAuditService
{
    Task LogAsync(
        AuditEntityType entityType,
        string entityId,
        AuditAction action,
        ActorType actorType = ActorType.Customer,
        string? actorIdentityId = null,
        string? customerId = null,
        object? details = null,
        string? ipAddress = null,
        string? correlationId = null);
}

public class AuditService : IAuditService
{
    private readonly JewBankDbContext _dbContext;

    public AuditService(JewBankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(
        AuditEntityType entityType,
        string entityId,
        AuditAction action,
        ActorType actorType = ActorType.Customer,
        string? actorIdentityId = null,
        string? customerId = null,
        object? details = null,
        string? ipAddress = null,
        string? correlationId = null)
    {
        var log = new AuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorType = actorType,
            ActorIdentityId = actorIdentityId,
            CustomerId = customerId,
            OccurredAt = DateTimeOffset.UtcNow,
            IpAddress = ipAddress,
            CorrelationId = correlationId,
            DetailsJson = details != null ? JsonSerializer.Serialize(details) : null
        };

        await _dbContext.AuditLogs.AddAsync(log);
        await _dbContext.SaveChangesAsync();
    }
}
