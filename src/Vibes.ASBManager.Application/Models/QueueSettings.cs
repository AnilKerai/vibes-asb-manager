namespace Vibes.ASBManager.Application.Models;

public sealed class QueueSettings
{
    public required string Name { get; init; }
    public required TimeSpan DefaultMessageTimeToLive { get; set; }
    public bool DeadLetteringOnMessageExpiration { get; set; }
    // Advanced properties
    public TimeSpan LockDuration { get; set; }
    public int MaxDeliveryCount { get; set; }
    public bool EnableBatchedOperations { get; set; }
    public string? ForwardTo { get; set; }
    public string? ForwardDeadLetteredMessagesTo { get; set; }
}
