namespace Vibes.ASBManager.Web.Models;

public class SendMessageRequest
{
    public string Body { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? CorrelationId { get; set; }
    public string? ContentType { get; set; } = "application/json";
    public Dictionary<string, string> Properties { get; set; } = new();
    public int Count { get; set; } = 1;
    public int IntervalSeconds { get; set; }
    public DateTimeOffset? ScheduledEnqueueUtc { get; set; }
}
