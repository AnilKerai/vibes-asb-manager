namespace Vibes.ASBManager.Web.Models;

public class SendMessageRequest
{
    public string Body { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? CorrelationId { get; set; }
    public string? ContentType { get; set; } = "application/json";
    public Dictionary<string, string> Properties { get; set; } = new();
    public int Count { get; set; } = 1;
    // Optional interval between messages in seconds; <= 0 means send immediately back-to-back
    public int IntervalSeconds { get; set; } = 0;
}
