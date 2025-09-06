namespace Vibes.ASBManager.Application.Models;

public sealed class TopicSettings
{
    public required string Name { get; init; }
    public required System.TimeSpan DefaultMessageTimeToLive { get; set; }
}
