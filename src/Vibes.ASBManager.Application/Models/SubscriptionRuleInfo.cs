namespace Vibes.ASBManager.Application.Models;

public class SubscriptionRuleInfo
{
    public string Name { get; set; } = string.Empty;
    public string Filter { get; set; } = string.Empty;
    public string? Action { get; set; }
}
