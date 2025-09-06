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
    /// Whether this connection is pinned/starred. Pinned connections are shown first in lists.
    /// </summary>
    public bool Pinned { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedUtc { get; set; }

}
