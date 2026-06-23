using Vibes.ASBManager.Application.Messaging;
using Vibes.ASBManager.Application.Models;
using Vibes.ASBManager.Web.Shared;

namespace Vibes.ASBManager.Tests.Unit.Web.Shared;

// Covers the pure decision logic extracted out of EntitiesView into MessageBrowsingController (C1).
// The stateful browse/live/purge flows need a broker and are exercised by the emulator integration tests.
public class MessageBrowsingControllerTests
{
    private static MessagePreview Msg(long seq) => new() { SequenceNumber = seq, EnqueuedTime = DateTimeOffset.UnixEpoch };

    [Fact]
    public void SnapshotTarget_uses_fetch_size_when_count_unknown()
        => Assert.Equal(50, MessageBrowsingController.SnapshotTarget(null, fetchSize: 50, maxSnapshot: 500));

    [Fact]
    public void SnapshotTarget_uses_the_known_count_within_range()
        => Assert.Equal(120, MessageBrowsingController.SnapshotTarget(120, fetchSize: 50, maxSnapshot: 500));

    [Fact]
    public void SnapshotTarget_clamps_to_the_max()
        => Assert.Equal(500, MessageBrowsingController.SnapshotTarget(10_000, fetchSize: 50, maxSnapshot: 500));

    [Fact]
    public void SnapshotTarget_clamps_a_negative_count_to_zero()
        => Assert.Equal(0, MessageBrowsingController.SnapshotTarget(-5, fetchSize: 50, maxSnapshot: 500));

    [Fact]
    public void ReconcileActiveFromDlq_removes_messages_now_in_the_dlq()
    {
        var active = new[] { Msg(1), Msg(2), Msg(3) };
        var dlq = new[] { Msg(2) };

        var result = MessageBrowsingController.ReconcileActiveFromDlq(active, dlq);

        Assert.Equal(new long[] { 1, 3 }, result.Select(m => m.SequenceNumber));
    }

    [Fact]
    public void ReconcileActiveFromDlq_returns_active_unchanged_when_dlq_empty()
    {
        var active = new[] { Msg(1), Msg(2) };

        var result = MessageBrowsingController.ReconcileActiveFromDlq(active, Array.Empty<MessagePreview>());

        Assert.Equal(new long[] { 1, 2 }, result.Select(m => m.SequenceNumber));
    }

    [Fact]
    public void NotifyOutcome_success_clears_the_tracked_error()
    {
        var (shouldToast, lastError) = MessageBrowsingController.NotifyOutcome("previous failure", null);
        Assert.False(shouldToast);
        Assert.Null(lastError);
    }

    [Fact]
    public void NotifyOutcome_suppresses_a_repeated_error()
    {
        var (shouldToast, lastError) = MessageBrowsingController.NotifyOutcome("boom", "boom");
        Assert.False(shouldToast);
        Assert.Equal("boom", lastError);
    }

    [Fact]
    public void NotifyOutcome_surfaces_a_new_error()
    {
        var (shouldToast, lastError) = MessageBrowsingController.NotifyOutcome("boom", "different");
        Assert.True(shouldToast);
        Assert.Equal("different", lastError);
    }

    [Fact]
    public void PurgeResultMessage_reports_the_count()
    {
        var message = MessageBrowsingController.PurgeResultMessage(42, "q-orders", isDeadLetter: false);
        Assert.Equal("Purged 42 messages from q-orders.", message);
    }

    [Fact]
    public void PurgeResultMessage_labels_dead_letter_messages()
    {
        var message = MessageBrowsingController.PurgeResultMessage(7, "q-orders", isDeadLetter: true);
        Assert.Contains("DLQ messages", message);
    }

    [Fact]
    public void PurgeResultMessage_warns_more_may_remain_at_the_ceiling()
    {
        var message = MessageBrowsingController.PurgeResultMessage(MessagingDefaults.PurgeCeiling, "q-orders", isDeadLetter: false);
        Assert.Contains("more may remain", message);
    }
}
