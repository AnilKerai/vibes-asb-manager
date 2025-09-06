namespace Vibes.ASBManager.Application.Models;

public sealed class TopicSummary
{
    public required string Name { get; init; }
    public int SubscriptionCount { get; init; }
    public long ScheduledMessageCount { get; init; }
}
