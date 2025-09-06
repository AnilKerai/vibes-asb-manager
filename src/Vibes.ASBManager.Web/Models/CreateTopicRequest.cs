namespace Vibes.ASBManager.Web.Models;

public class CreateTopicRequest
{
    public string Name { get; set; } = string.Empty;
    public string? TtlText { get; set; }
    public bool EnableBatchedOperations { get; set; } = true;
}
