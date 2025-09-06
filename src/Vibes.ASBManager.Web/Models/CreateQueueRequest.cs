namespace Vibes.ASBManager.Web.Models;

public class CreateQueueRequest
{
    public string Name { get; set; } = string.Empty;
    public string? TtlText { get; set; }
    public bool DeadLetterOnExpiration { get; set; }

    // Advanced
    public bool RequiresSession { get; set; } = false;
    public string? LockDurationText { get; set; } = "00:01:00"; // 1 minute
    public int MaxDeliveryCount { get; set; } = 10;
    public bool EnableBatchedOperations { get; set; } = true;
    public string? ForwardTo { get; set; }
    public string? ForwardDeadLetteredMessagesTo { get; set; }
}
