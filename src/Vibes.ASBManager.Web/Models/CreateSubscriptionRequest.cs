namespace Vibes.ASBManager.Web.Models;

public class CreateSubscriptionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? TtlText { get; set; }
    public bool DeadLetterOnExpiration { get; set; }

    // Advanced
    public bool RequiresSession { get; set; } = false;
    public string? LockDurationText { get; set; } = "00:01:00"; // 1 minute default
    public int MaxDeliveryCount { get; set; } = 10; // Azure default
    public bool EnableBatchedOperations { get; set; } = true;
    public string? ForwardTo { get; set; }
    public string? ForwardDeadLetteredMessagesTo { get; set; }

    // Initial rule
    // Modes: "MatchAll" or "Sql"
    public string InitialRuleMode { get; set; } = "MatchAll";
    public string? InitialRuleSql { get; set; }
    public string? InitialRuleSqlAction { get; set; }
}
