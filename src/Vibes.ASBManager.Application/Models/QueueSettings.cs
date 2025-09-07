namespace Vibes.ASBManager.Application.Models;

public sealed class QueueSettings
{
    public required string Name { get; init; }
    public required TimeSpan DefaultMessageTimeToLive { get; set; }
    public bool DeadLetteringOnMessageExpiration { get; set; }
}
