namespace Vibes.ASBManager.Domain.Models;

/// <summary>
/// Represents a saved Azure Service Bus connection.
/// </summary>
public sealed class ConnectionInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The full connection string for the namespace.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// User-defined tags for filtering, e.g. env:prod, svc:orders
    /// </summary>
    public List<string> Tags { get; set; } = new();

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedUtc { get; set; }

    public void NormalizeTags()
    {
        if (Tags.Count == 0) return;
        Tags = Tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }
}
