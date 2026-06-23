using Microsoft.Extensions.Logging;
using MudBlazor;
using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Web.Shared;

public partial class EntitiesView
{
    // Messages view state
    private List<MessagePreview> _activeMessages = new();
    private List<MessagePreview> _dlqMessages = new();
    // 0 = idle, 1 = a refresh is in flight. Guarded with Interlocked because a refresh can be
    // triggered concurrently from a background polling loop and from a UI action.
    private int _refreshingActive;
    private int _refreshingDlq;

    // Last error surfaced per refresh op; suppresses duplicate toasts when a failure persists
    // across poll ticks (C3). null = healthy.
    private string? _activeRefreshError;
    private string? _dlqRefreshError;
    private string? _countsRefreshError;

    private const int FetchSize = 50; // how many messages to peek per API call

    // Upper bound on a single snapshot. Peeking is non-destructive but not free, so we cap
    // how many messages we pull; the runtime count drives the actual target up to this.
    private const int MaxSnapshotMessages = FetchSize * 10; // 500

    // PeekMessagesAsync can briefly return an empty batch on a freshly created receiver
    // even when messages exist; only treat repeated empties as "end of queue".
    private const int MaxEmptyPeeks = 3;

    private bool _liveActive;
    private bool _liveDlq;
    private CancellationTokenSource? _liveActiveCts;
    private CancellationTokenSource? _liveDlqCts;
    private bool _pendingActiveClear;
    private bool _pendingDlqClear;
    private long? _activeCount;
    private long? _dlqCount;
    private CancellationTokenSource? _countsCts;
    private CancellationTokenSource? _sendCts;

    // Purge progress/cancellation. A purge drains to empty (bounded by MessagingDefaults.PurgeCeiling),
    // so it can take a while on a large entity; surface a running count and cancel it on dispose.
    private bool _purgingActive;
    private bool _purgingDlq;
    private int _purgeProgress;
    private CancellationTokenSource? _purgeCts;
    private volatile bool _disposed; // set on Dispose; read by the background polling loops
    private const int CountsRefreshIntervalMs = 2000;
    private const int LiveRefreshIntervalMs = 2000;

    // Pull an authoritative snapshot of the selected entity's current messages, capping at the
    // runtime count (or FetchSize when the count is unknown). Paging runs behind a single receiver
    // in the infra layer (IMessageBrowser snapshot methods); the pure paging algorithm it uses is
    // unit-tested separately as MessageSnapshotPager.
    private async Task<List<MessagePreview>> PeekSnapshotAsync(bool isDeadLetter, long? knownCount, CancellationToken cancellationToken)
    {
        var target = knownCount.HasValue
            ? (int)Math.Clamp(knownCount.Value, 0, MaxSnapshotMessages)
            : FetchSize;
        if (target <= 0) return new List<MessagePreview>();

        IReadOnlyList<MessagePreview> snapshot;
        if (TryGetQueue(out var queueName))
        {
            snapshot = isDeadLetter
                ? await MessageBrowser.PeekQueueDeadLetterSnapshotAsync(_connectionString!, queueName, target, FetchSize, MaxEmptyPeeks, cancellationToken)
                : await MessageBrowser.PeekQueueSnapshotAsync(_connectionString!, queueName, target, FetchSize, MaxEmptyPeeks, cancellationToken);
        }
        else if (TryGetSubscription(out var topicName, out var subscriptionName))
        {
            snapshot = isDeadLetter
                ? await MessageBrowser.PeekSubscriptionDeadLetterSnapshotAsync(_connectionString!, topicName, subscriptionName, target, FetchSize, MaxEmptyPeeks, cancellationToken)
                : await MessageBrowser.PeekSubscriptionSnapshotAsync(_connectionString!, topicName, subscriptionName, target, FetchSize, MaxEmptyPeeks, cancellationToken);
        }
        else
        {
            return new List<MessagePreview>();
        }
        return snapshot.ToList();
    }

    private CancellationToken StartSendCancellation()
    {
        StopSend();
        _sendCts = new CancellationTokenSource();
        return _sendCts.Token;
    }

    private void StopSend()
    {
        try { _sendCts?.Cancel(); }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to cancel send operation.");
        }
        try { _sendCts?.Dispose(); }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to dispose send cancellation token.");
        }
        _sendCts = null;
    }

    private CancellationToken StartPurgeCancellation()
    {
        StopPurge();
        _purgeCts = new CancellationTokenSource();
        return _purgeCts.Token;
    }

    private void StopPurge()
    {
        try { _purgeCts?.Cancel(); }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to cancel purge operation.");
        }
        try { _purgeCts?.Dispose(); }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to dispose purge cancellation token.");
        }
        _purgeCts = null;
    }

    // Active/DLQ refresh and live polling

    // Surface a refresh failure once; suppress repeats of the same message so a persistent failure
    // doesn't toast on every poll tick. Returns the new "last error" state (null = healthy).
    private string? NotifyRefreshOutcome(string? lastError, string? message)
    {
        if (message is null) return null;
        if (string.Equals(message, lastError, StringComparison.Ordinal)) return lastError;
        Snackbar.Add(message, Severity.Error);
        return message;
    }

    // Runs tickAsync immediately, then on a fixed cadence on a background task, until the token is
    // cancelled or the component is disposed. Uses PeriodicTimer (no per-iteration Task.Delay).
    private void RunPollLoop(CancellationToken token, int intervalMs, Func<CancellationToken, Task> tickAsync)
    {
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));
            try
            {
                do
                {
                    await tickAsync(token);
                }
                while (!_disposed && !token.IsCancellationRequested && await timer.WaitForNextTickAsync(token));
            }
            catch (OperationCanceledException)
            {
                // expected when polling is stopped or the component is disposed
            }
        });
    }

    private async Task RefreshAndRenderActiveAsync(CancellationToken token)
    {
        await RefreshActiveAsync(token);
        if (_disposed || token.IsCancellationRequested) return;
        try { await InvokeAsync(StateHasChanged); }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to refresh UI during live active polling.");
        }
    }

    private async Task RefreshAndRenderDlqAsync(CancellationToken token)
    {
        await RefreshDlqAsync(token);
        if (_disposed || token.IsCancellationRequested) return;
        try { await InvokeAsync(StateHasChanged); }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to refresh UI during live DLQ polling.");
        }
    }

    private async Task RefreshActiveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRefreshMessages || cancellationToken.IsCancellationRequested) return;
        if (Interlocked.CompareExchange(ref _refreshingActive, 1, 0) != 0) return; // already refreshing
        try
        {
            if (_pendingActiveClear)
            {
                _activeMessages.Clear();
                _pendingActiveClear = false;
            }
            var snapshot = await PeekSnapshotAsync(isDeadLetter: false, _activeCount, cancellationToken);
            _activeMessages = snapshot
                .OrderBy(m => m.EnqueuedTime)
                .ToList();
            ReconcileActiveFromDlq();
            _activeRefreshError = NotifyRefreshOutcome(_activeRefreshError, null);
        }
        catch (Exception ex)
        {
            _activeRefreshError = NotifyRefreshOutcome(_activeRefreshError, $"Failed to refresh active messages: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshingActive, 0);
        }
    }

    private async Task RefreshDlqAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRefreshMessages || cancellationToken.IsCancellationRequested) return;
        if (Interlocked.CompareExchange(ref _refreshingDlq, 1, 0) != 0) return; // already refreshing
        try
        {
            if (_pendingDlqClear)
            {
                _dlqMessages.Clear();
                _pendingDlqClear = false;
            }
            var snapshot = await PeekSnapshotAsync(isDeadLetter: true, _dlqCount, cancellationToken);
            _dlqMessages = snapshot
                .OrderBy(m => m.EnqueuedTime)
                .ToList();
            ReconcileActiveFromDlq();
            _dlqRefreshError = NotifyRefreshOutcome(_dlqRefreshError, null);
        }
        catch (Exception ex)
        {
            _dlqRefreshError = NotifyRefreshOutcome(_dlqRefreshError, $"Failed to refresh DLQ messages: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshingDlq, 0);
        }
    }

    // Conditional refresh helpers for purge actions
    private async Task RefreshActiveIfNeededAsync()
    {
        if (CanRefreshMessages && _activeMessages is not null && _activeMessages.Count > 0)
        {
            await RefreshActiveAsync();
            if (!_disposed)
            {
                try { await InvokeAsync(StateHasChanged); }
                catch (Exception ex)
                {
                    Logger?.LogDebug(ex, "Failed to refresh UI after active refresh.");
                }
            }
        }
    }

    private async Task RefreshDlqIfNeededAsync()
    {
        if (CanRefreshMessages && _dlqMessages is not null && _dlqMessages.Count > 0)
        {
            await RefreshDlqAsync();
            if (!_disposed)
            {
                try { await InvokeAsync(StateHasChanged); }
                catch (Exception ex)
                {
                    Logger?.LogDebug(ex, "Failed to refresh UI after DLQ refresh.");
                }
            }
        }
    }

    private async Task RefreshCountsAsync(CancellationToken cancellationToken = default)
    {
        if (!HasConnection || string.IsNullOrEmpty(_selectedTreeValue)) { _activeCount = null; _dlqCount = null; return; }
        if (cancellationToken.IsCancellationRequested) return;
        try
        {
            if (TryGetQueue(out var q))
            {
                var qs = await QueueAdmin.GetQueueRuntimeAsync(_connectionString!, q, cancellationToken);
                _activeCount = qs?.ActiveMessageCount;
                _dlqCount = qs?.DeadLetterMessageCount;
            }
            else if (TryGetSubscription(out var t, out var s))
            {
                var ss = await SubscriptionAdmin.GetSubscriptionRuntimeAsync(_connectionString!, t, s, cancellationToken);
                _activeCount = ss?.ActiveMessageCount;
                _dlqCount = ss?.DeadLetterMessageCount;
            }
            else
            {
                _activeCount = null;
                _dlqCount = null;
            }

            // If counts indicate zero, clear corresponding lists to avoid stale rows
            if ((_activeCount ?? 0) == 0)
            {
                _activeMessages.Clear();
            }
            if ((_dlqCount ?? 0) == 0)
            {
                _dlqMessages.Clear();
            }
            _countsRefreshError = NotifyRefreshOutcome(_countsRefreshError, null);
        }
        catch (Exception ex)
        {
            _countsRefreshError = NotifyRefreshOutcome(_countsRefreshError, $"Failed to refresh counts: {ex.Message}");
        }
        finally
        {
            if (!_disposed)
            {
                try { await InvokeAsync(StateHasChanged); }
                catch (Exception ex)
                {
                    Logger?.LogDebug(ex, "Failed to refresh UI after count refresh.");
                }
            }
        }
    }

    private async Task OnActiveRefresh()
    {
        if (!CanRefreshMessages) return;
        await RefreshCountsAsync();
        await RefreshActiveAsync();
        if (!_disposed)
        {
            try { await InvokeAsync(StateHasChanged); }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "Failed to refresh UI after active refresh.");
            }
        }
    }

    private async Task OnDlqRefresh()
    {
        if (!CanRefreshMessages) return;
        await RefreshCountsAsync();
        await RefreshDlqAsync();
        if (!_disposed)
        {
            try { await InvokeAsync(StateHasChanged); }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "Failed to refresh UI after DLQ refresh.");
            }
        }
    }

    private async Task ToggleLiveActiveAsync()
    {
        if (!_liveActive)
        {
            StartLiveActive();
        }
        else
        {
            StopLiveActive();
        }
        await Task.CompletedTask;
    }

    private async Task ToggleLiveDlqAsync()
    {
        if (!_liveDlq)
        {
            StartLiveDlq();
        }
        else
        {
            StopLiveDlq();
        }
        await Task.CompletedTask;
    }

    private async Task ToggleLiveBothAsync()
    {
        // Toggle both active and DLQ live polling together from the top action bar
        var liveNow = _liveActive || _liveDlq;
        if (!liveNow)
        {
            StartLiveActive();
            StartLiveDlq();
        }
        else
        {
            StopLiveActive();
            StopLiveDlq();
        }
        await Task.CompletedTask;
    }

    private void StartLiveActive()
    {
        if (!CanRefreshMessages || _disposed) return;
        StopLiveActive();
        _liveActive = true;
        _liveActiveCts = new CancellationTokenSource();
        RunPollLoop(_liveActiveCts.Token, LiveRefreshIntervalMs, RefreshAndRenderActiveAsync);
    }

    private void StopLiveActive()
    {
        _liveActive = false;
        try { _liveActiveCts?.Cancel(); }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to cancel live active polling.");
        }
        try { _liveActiveCts?.Dispose(); }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to dispose live active cancellation token.");
        }
        _liveActiveCts = null;
    }

    private void StartLiveDlq()
    {
        if (!CanRefreshMessages || _disposed) return;
        StopLiveDlq();
        _liveDlq = true;
        _liveDlqCts = new CancellationTokenSource();
        RunPollLoop(_liveDlqCts.Token, LiveRefreshIntervalMs, RefreshAndRenderDlqAsync);
    }

    private void StopLiveDlq()
    {
        _liveDlq = false;
        try { _liveDlqCts?.Cancel(); }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to cancel live DLQ polling.");
        }
        try { _liveDlqCts?.Dispose(); }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to dispose live DLQ cancellation token.");
        }
        _liveDlqCts = null;
    }

    private void StartCountsPolling()
    {
        if (string.IsNullOrEmpty(_selectedTreeValue) || _disposed) return;
        StopCountsPolling();
        _countsCts = new CancellationTokenSource();
        RunPollLoop(_countsCts.Token, CountsRefreshIntervalMs, RefreshCountsAsync);
    }

    private void StopCountsPolling()
    {
        try { _countsCts?.Cancel(); }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to cancel counts polling.");
        }
        try { _countsCts?.Dispose(); }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to dispose counts polling cancellation token.");
        }
        _countsCts = null;
    }

    private void ReconcileActiveFromDlq()
    {
        // Remove from active list any messages that are now present in DLQ
        if (_activeMessages is null || _activeMessages.Count == 0) return;
        if (_dlqMessages is null || _dlqMessages.Count == 0) return;
        var dlqSeq = _dlqMessages.Select(m => m.SequenceNumber).ToHashSet();
        if (dlqSeq.Count == 0) return;
        _activeMessages = _activeMessages.Where(m => !dlqSeq.Contains(m.SequenceNumber)).ToList();
    }

    private async Task OnActiveRowClick(TableRowClickEventArgs<MessagePreview> args)
    {
        if (args?.Item is null) return;
        await ShowMessageDetailsAsync(args.Item.SequenceNumber, isDeadLetter: false);
    }

    private async Task OnDlqRowClick(TableRowClickEventArgs<MessagePreview> args)
    {
        if (args?.Item is null) return;
        await ShowMessageDetailsAsync(args.Item.SequenceNumber, isDeadLetter: true);
    }

    private async Task ShowMessageDetailsAsync(long sequenceNumber, bool isDeadLetter)
    {
        if (string.IsNullOrWhiteSpace(_connectionString)) return;
        try
        {
            MessageDetails? details = null;
            if (TryGetQueue(out var q))
            {
                details = isDeadLetter
                    ? await MessageBrowser.PeekQueueDeadLetterMessageAsync(_connectionString!, q, sequenceNumber)
                    : await MessageBrowser.PeekQueueMessageAsync(_connectionString!, q, sequenceNumber);
            }
            else if (TryGetSubscription(out var t, out var s))
            {
                details = isDeadLetter
                    ? await MessageBrowser.PeekSubscriptionDeadLetterMessageAsync(_connectionString!, t, s, sequenceNumber)
                    : await MessageBrowser.PeekSubscriptionMessageAsync(_connectionString!, t, s, sequenceNumber);
            }

            if (details is null)
            {
                Snackbar.Add("Message not found. It may have moved or expired.", Severity.Info);
                return;
            }

            var parameters = new DialogParameters
            {
                ["Details"] = details,
                ["IsDeadLetter"] = isDeadLetter,
                ["ConnectionString"] = _connectionString!
            };
            if (TryGetQueue(out var qq))
            {
                parameters["QueueName"] = qq;
            }
            else if (TryGetSubscription(out var tt, out var ss))
            {
                parameters["TopicName"] = tt;
                parameters["SubscriptionName"] = ss;
            }
            var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Large, FullWidth = true };
            var dialog = await DialogService.ShowAsync<MessageDetailsDialog>("Message Details", parameters, options);
            var result = await dialog.Result;
            if (result is { Canceled: false, Data: bool changed } && changed)
            {
                // If message was resubmitted from DLQ, both lists could change
                await RefreshActiveIfNeededAsync();
                await RefreshDlqIfNeededAsync();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to fetch message details: {ex.Message}", Severity.Error);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        StopLiveActive();
        StopLiveDlq();
        StopCountsPolling();
        StopSend();
        StopPurge();
    }
}
