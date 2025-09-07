namespace Vibes.ASBManager.Web.Models;

public class CreateSubscriptionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? TtlText { get; set; }
    public bool DeadLetterOnExpiration { get; set; }
    public bool RequiresSession { get; set; }
    public string? LockDurationText { get; set; } = "00:01:00";
    public int MaxDeliveryCount { get; set; } = 10;
    public bool EnableBatchedOperations { get; set; } = true;
    public string? ForwardTo { get; set; }
    public string? ForwardDeadLetteredMessagesTo { get; set; }
    public string InitialRuleMode { get; set; } = "MatchAll";
    public string? InitialRuleSql { get; set; }
    public string? InitialRuleSqlAction { get; set; }
}
