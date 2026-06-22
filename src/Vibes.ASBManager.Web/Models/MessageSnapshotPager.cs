using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Web.Models;

/// <summary>
/// Pages through Azure Service Bus peek results to build an authoritative snapshot of an
/// entity's current messages.
/// <para>
/// <c>PeekMessagesAsync</c> may return fewer messages than requested — or briefly none — on
/// any single call, especially on a freshly created receiver. Treating a short or empty batch
/// as "end of queue" is what made the message tables show an arbitrary handful of rows (or
/// none) instead of the real contents. This pager keeps requesting pages, advancing past the
/// highest sequence number seen, until it has gathered the requested count, reached repeated
/// empty batches, or stopped making forward progress.
/// </para>
/// </summary>
public static class MessageSnapshotPager
{
    /// <summary>
    /// Peeks a single page of up to <paramref name="maxMessages"/> messages starting at
    /// <paramref name="fromSequenceNumber"/> (<c>null</c> = from the head of the queue).
    /// </summary>
    public delegate Task<IReadOnlyList<MessagePreview>> PeekPage(long? fromSequenceNumber, int maxMessages, CancellationToken cancellationToken);

    /// <summary>
    /// The sequence number to peek from next, given the page just read. Returns the current
    /// anchor unchanged for an empty page, and clamps at <see cref="long.MaxValue"/>.
    /// </summary>
    public static long? GetNextAnchor(long? currentAnchor, IReadOnlyList<MessagePreview> page)
    {
        if (page.Count == 0) return currentAnchor;
        var maxSeq = page.Max(m => m.SequenceNumber);
        return maxSeq == long.MaxValue ? long.MaxValue : maxSeq + 1;
    }

    /// <summary>
    /// Collects up to <paramref name="target"/> messages by paging from the head with
    /// <paramref name="peek"/>, tolerating short and transient-empty batches.
    /// </summary>
    /// <param name="peek">Reads one page starting at a given sequence number.</param>
    /// <param name="target">Desired number of messages (e.g. the entity's runtime count).</param>
    /// <param name="fetchSize">Page size requested per peek.</param>
    /// <param name="maxEmptyPeeks">Consecutive empty pages tolerated before concluding the end of the queue.</param>
    public static async Task<List<MessagePreview>> CollectAsync(
        PeekPage peek,
        int target,
        int fetchSize,
        int maxEmptyPeeks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(peek);
        if (target <= 0 || fetchSize <= 0) return new List<MessagePreview>();

        var collected = new List<MessagePreview>();
        var seen = new HashSet<long>();
        long? anchor = null; // always snapshot from the head
        var emptyPeeks = 0;
        var iterations = 0;
        // Backstop against a misbehaving peek; real termination is the target, repeated
        // empties, or a non-advancing anchor. Generous so legitimate short batches still page.
        var maxIterations = target + maxEmptyPeeks + 8;

        while (collected.Count < target && !cancellationToken.IsCancellationRequested && iterations++ < maxIterations)
        {
            var page = await peek(anchor, fetchSize, cancellationToken).ConfigureAwait(false);
            if (page.Count == 0)
            {
                // A short or empty batch does NOT mean the queue is empty — retry a few times
                // before concluding we've reached the end.
                if (++emptyPeeks > maxEmptyPeeks) break;
                continue;
            }

            emptyPeeks = 0;
            foreach (var message in page)
            {
                if (seen.Add(message.SequenceNumber))
                    collected.Add(message);
            }

            var nextAnchor = GetNextAnchor(anchor, page);
            if (nextAnchor == anchor) break; // no forward progress (e.g. at long.MaxValue)
            anchor = nextAnchor;
        }

        return collected;
    }
}
