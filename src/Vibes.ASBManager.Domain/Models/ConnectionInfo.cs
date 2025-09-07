namespace Vibes.ASBManager.Domain.Models;

public sealed class ConnectionInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Name { get; set; } = string.Empty;
    public string? ConnectionString { get; set; }
    public bool Pinned { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedUtc { get; set; }
}
