namespace Vibes.ASBManager.Application.Models;

public sealed class SubscriptionSettings
{
    public required string TopicName { get; init; }
    public required string SubscriptionName { get; init; }
    public required TimeSpan DefaultMessageTimeToLive { get; set; }
    public bool DeadLetteringOnMessageExpiration { get; set; }
    // Advanced properties
    public bool RequiresSession { get; set; }
    public TimeSpan LockDuration { get; set; }
    public int MaxDeliveryCount { get; set; }
    public bool EnableBatchedOperations { get; set; }
    public string? ForwardTo { get; set; }
    public string? ForwardDeadLetteredMessagesTo { get; set; }
}
