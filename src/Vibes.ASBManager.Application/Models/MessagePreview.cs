namespace Vibes.ASBManager.Application.Models;

public sealed class MessagePreview
{
    public long SequenceNumber { get; init; }
    public DateTimeOffset EnqueuedTime { get; init; }
    public string? MessageId { get; init; }
    public string? Subject { get; init; }
    public string? CorrelationId { get; init; }
}
