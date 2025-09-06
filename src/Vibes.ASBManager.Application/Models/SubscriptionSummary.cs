namespace Vibes.ASBManager.Application.Models;

public sealed class SubscriptionSummary
{
    public required string TopicName { get; init; }
    public required string SubscriptionName { get; init; }
    public long ActiveMessageCount { get; init; }
    public long DeadLetterMessageCount { get; init; }
}
