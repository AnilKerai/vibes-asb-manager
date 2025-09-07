namespace Vibes.ASBManager.Application.Models;

public sealed class TopicSettings
{
    public required string Name { get; init; }
    public required TimeSpan DefaultMessageTimeToLive { get; set; }
}
