namespace JewBank.Models;

using JewBank.Enums;

public class AuditLog
{
    public required string AuditId { get; init; }
    public string? ActorIdentityId { get; init; }
    public ActorType ActorType { get; init; }
    public string? CustomerId { get; init; }

    public AuditEntityType EntityType { get; init; }
    public required string EntityId { get; init; }
    public AuditAction Action { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }
    public string? IpAddress { get; init; }
    public string? DetailsJson { get; init; }
}