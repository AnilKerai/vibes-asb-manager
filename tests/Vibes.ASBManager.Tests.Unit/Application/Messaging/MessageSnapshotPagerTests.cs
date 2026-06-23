using Vibes.ASBManager.Application.Models;
using static Vibes.ASBManager.Application.Messaging.MessageSnapshotPager;

namespace Vibes.ASBManager.Tests.Unit.Application.Messaging;

public class MessageSnapshotPagerTests
{
    private static MessagePreview Msg(long seq)
        // Bound the offset so seq = long.MaxValue doesn't overflow DateTimeOffset; the exact
        // time is irrelevant to the pager (it never orders by it).
        => new() { SequenceNumber = seq, EnqueuedTime = DateTimeOffset.UnixEpoch.AddSeconds(seq % 86_400) };

    private static IReadOnlyList<MessagePreview> Page(long start, int count)
        => Enumerable.Range(0, count).Select(i => Msg(start + i)).ToList();

    // Returns each canned page in turn, then empty pages forever (a fresh receiver that has
    // run dry). Ignores the anchor, so it can model any sequence of peek results.
    private static PeekPage Scripted(params IReadOnlyList<MessagePreview>[] pages)
    {
        var queue = new Queue<IReadOnlyList<MessagePreview>>(pages);
        return (_, _, _) => Task.FromResult(queue.Count > 0 ? queue.Dequeue() : Array.Empty<MessagePreview>());
    }

    // Realistic store that honours fromSequenceNumber, optionally capping each page to simulate
    // partial batches. Records the anchor each call was made with.
    private static PeekPage Store(IEnumerable<long> sequenceNumbers, int? perCallCap = null, IList<long?>? anchors = null)
    {
        var store = sequenceNumbers.Select(Msg).OrderBy(m => m.SequenceNumber).ToList();
        return (from, max, _) =>
        {
            anchors?.Add(from);
            var take = perCallCap.HasValue ? Math.Min(max, perCallCap.Value) : max;
            IReadOnlyList<MessagePreview> page = store
                .Where(m => !from.HasValue || m.SequenceNumber >= from.Value)
                .Take(take)
                .ToList();
            return Task.FromResult(page);
        };
    }

    [Fact]
    public async Task Does_not_stop_on_a_short_first_batch()
    {
        // The bug: peek returns a partial first batch (2), and the old loop broke on
        // page.Count < FetchSize — showing only 2 rows of a 100-message queue.
        var peek = Scripted(Page(1, 2), Page(3, 50), Page(53, 48));

        var result = await CollectAsync(peek, target: 100, fetchSize: 50, maxEmptyPeeks: 3);

        Assert.Equal(100, result.Count);
    }

    [Fact]
    public async Task Retries_a_transient_empty_batch()
    {
        // The other half of the bug: a cold receiver returns nothing on the first peek, and
        // the old loop concluded the queue was empty — showing no rows.
        var peek = Scripted(Array.Empty<MessagePreview>(), Page(1, 10));

        var result = await CollectAsync(peek, target: 10, fetchSize: 50, maxEmptyPeeks: 3);

        Assert.Equal(10, result.Count);
    }

    [Fact]
    public async Task Gives_up_after_repeated_empty_batches()
    {
        var calls = 0;
        PeekPage peek = (_, _, _) =>
        {
            calls++;
            return Task.FromResult<IReadOnlyList<MessagePreview>>(Array.Empty<MessagePreview>());
        };

        var result = await CollectAsync(peek, target: 100, fetchSize: 50, maxEmptyPeeks: 3);

        Assert.Empty(result);
        Assert.Equal(4, calls); // maxEmptyPeeks tolerated, then one more that trips the limit
    }

    [Fact]
    public async Task Stops_once_target_is_reached()
    {
        var peek = Store(Enumerable.Range(1, 1000).Select(i => (long)i));

        var result = await CollectAsync(peek, target: 100, fetchSize: 50, maxEmptyPeeks: 3);

        Assert.Equal(100, result.Count);
        Assert.Equal(Enumerable.Range(1, 100).Select(i => (long)i), result.Select(m => m.SequenceNumber));
    }

    [Fact]
    public async Task Collects_everything_when_target_exceeds_what_exists()
    {
        var peek = Store(Enumerable.Range(1, 30).Select(i => (long)i));

        var result = await CollectAsync(peek, target: 500, fetchSize: 50, maxEmptyPeeks: 3);

        Assert.Equal(30, result.Count);
    }

    [Fact]
    public async Task Pages_through_consistently_short_batches()
    {
        // Every peek returns only 5, far below fetchSize — must still gather all 40.
        var peek = Store(Enumerable.Range(1, 40).Select(i => (long)i), perCallCap: 5);

        var result = await CollectAsync(peek, target: 40, fetchSize: 50, maxEmptyPeeks: 3);

        Assert.Equal(40, result.Count);
    }

    [Fact]
    public async Task Deduplicates_overlapping_pages()
    {
        var peek = Scripted(Page(1, 50), Page(45, 50)); // sequences 45-50 appear twice

        var result = await CollectAsync(peek, target: 100, fetchSize: 50, maxEmptyPeeks: 3);

        Assert.Equal(94, result.Count); // 1-94, no duplicates
        Assert.Equal(result.Count, result.Select(m => m.SequenceNumber).Distinct().Count());
    }

    [Fact]
    public async Task Pages_forward_by_advancing_the_anchor()
    {
        var anchors = new List<long?>();
        var peek = Store(Enumerable.Range(1, 200).Select(i => (long)i), anchors: anchors);

        await CollectAsync(peek, target: 150, fetchSize: 50, maxEmptyPeeks: 3);

        Assert.Equal(new long?[] { null, 51, 101 }, anchors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Non_positive_target_does_not_peek(int target)
    {
        var calls = 0;
        PeekPage peek = (_, _, _) =>
        {
            calls++;
            return Task.FromResult<IReadOnlyList<MessagePreview>>(Array.Empty<MessagePreview>());
        };

        var result = await CollectAsync(peek, target, fetchSize: 50, maxEmptyPeeks: 3);

        Assert.Empty(result);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Stops_when_cancelled()
    {
        using var cts = new CancellationTokenSource();
        var peek = Store(Enumerable.Range(1, 1000).Select(i => (long)i), anchors: null);
        // Cancel as soon as the first page is requested.
        PeekPage cancelling = (from, max, ct) =>
        {
            cts.Cancel();
            return peek(from, max, ct);
        };

        var result = await CollectAsync(cancelling, target: 500, fetchSize: 50, maxEmptyPeeks: 3, cancellationToken: cts.Token);

        Assert.True(result.Count <= 50);
    }

    [Fact]
    public void GetNextAnchor_keeps_current_anchor_for_an_empty_page()
        => Assert.Equal(42L, GetNextAnchor(42L, Array.Empty<MessagePreview>()));

    [Fact]
    public void GetNextAnchor_returns_max_sequence_plus_one()
        => Assert.Equal(11L, GetNextAnchor(null, new[] { Msg(5), Msg(10), Msg(7) }));

    [Fact]
    public void GetNextAnchor_clamps_at_long_max()
        => Assert.Equal(long.MaxValue, GetNextAnchor(null, new[] { Msg(long.MaxValue) }));
}
