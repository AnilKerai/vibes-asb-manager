namespace Vibes.ASBManager.Application.Models;

public sealed class SubscriptionSettings
{
    public required string TopicName { get; init; }
    public required string SubscriptionName { get; init; }
    public required TimeSpan DefaultMessageTimeToLive { get; set; }
    public bool DeadLetteringOnMessageExpiration { get; set; }
}
