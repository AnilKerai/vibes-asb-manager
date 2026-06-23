namespace Vibes.ASBManager.Application.Messaging;

public static class MessagingDefaults
{
    // Safety ceiling for a single purge. High enough that one click empties any realistic queue,
    // but bounded so a purge against a live or very large entity still terminates and reports rather
    // than looping indefinitely. If a purge returns exactly this many, more may remain — run it again.
    public const int PurgeCeiling = 100_000;
}
