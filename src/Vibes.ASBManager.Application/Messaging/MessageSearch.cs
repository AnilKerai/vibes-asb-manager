using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Application.Messaging;

// Case-insensitive substring search across a message preview's human-readable fields. An empty or
// whitespace term matches everything (i.e. no active filter). Kept pure so it can be unit-tested and
// shared: the DLQ table uses it to filter what's shown, and the details dialog uses the same predicate
// to build its prev/next navigation set so arrow keys stay within the search results.
public static class MessageSearch
{
    public static bool Matches(MessagePreview message, string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return true;
        if (message is null) return false;

        var t = term.Trim();
        return Contains(message.CorrelationId, t)
            || Contains(message.Subject, t)
            || Contains(message.DeadLetterReason, t)
            || Contains(message.MessageId, t)
            || Contains(message.Body, t);
    }

    private static bool Contains(string? value, string term)
        => value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);
}
