namespace JewBank.Models;

using JewBank.Enums;

public class AuditLog
{
    public required string AuditId { get; set; } = string.Empty;
    public string ActorIdentityId { get; set; } = string.Empty;
    public required string CustomerId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime OccurredAt { get; set; }
    public ActorType ActorType { get; set; }
}