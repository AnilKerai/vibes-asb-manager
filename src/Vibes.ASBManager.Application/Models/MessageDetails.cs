namespace Vibes.ASBManager.Application.Models;

public sealed class MessageDetails
{
    public long SequenceNumber { get; init; }
    public DateTimeOffset EnqueuedTime { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? ScheduledEnqueueTime { get; init; }
    public int DeliveryCount { get; init; }

    public string? MessageId { get; init; }
    public string? Subject { get; init; }
    public string? CorrelationId { get; init; }
    public string? ContentType { get; init; }
    public string? To { get; init; }
    public string? ReplyTo { get; init; }
    public string? SessionId { get; init; }
    public string? PartitionKey { get; init; }
    public string? ReplyToSessionId { get; init; }
    public TimeSpan TimeToLive { get; init; }

    public string Body { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, object?> ApplicationProperties { get; init; } = new Dictionary<string, object?>();

    public string? DeadLetterSource { get; init; }
    public string? DeadLetterReason { get; init; }
    public string? DeadLetterErrorDescription { get; init; }
}
