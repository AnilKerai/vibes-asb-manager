namespace Vibes.ASBManager.Web.Models;

public class CreateCorrelationRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? Subject { get; set; }
    public string? To { get; set; }
    public string? ReplyTo { get; set; }
    public string? ReplyToSessionId { get; set; }
    public string? SessionId { get; set; }
    public string? ContentType { get; set; }
    public Dictionary<string, string> ApplicationProperties { get; set; } = new();
}
