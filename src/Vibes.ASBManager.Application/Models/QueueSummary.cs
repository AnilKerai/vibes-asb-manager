namespace Vibes.ASBManager.Application.Models;

public sealed class QueueSummary
{
    public required string Name { get; init; }
    public long ActiveMessageCount { get; init; }
    public long DeadLetterMessageCount { get; init; }
}
