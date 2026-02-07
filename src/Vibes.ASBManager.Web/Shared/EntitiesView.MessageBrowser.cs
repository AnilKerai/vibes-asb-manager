using MudBlazor;
using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Web.Shared;

public partial class EntitiesView
{
    // Messages view state
    private List<MessagePreview> _activeMessages = new();
    private List<MessagePreview> _dlqMessages = new();
    private bool _refreshingActive;
    private bool _refreshingDlq;
    private const int FetchSize = 50; // how many messages to fetch per API call

    private long? _activeAnchor;
    private long? _dlqAnchor;
    private Stack<long?> _activeHistory = new();
    private Stack<long?> _dlqHistory = new();
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
    private bool _disposed;
    private const int CountsRefreshIntervalMs = 2000;

    private async Task<IReadOnlyList<MessagePreview>> PeekSelectedMessagesAsync(bool isDeadLetter, long? fromSequenceNumber, int maxMessages, CancellationToken cancellationToken = default)
    {
        if (TryGetQueue(out var queueName))
        {
            return isDeadLetter
                ? await MessageBrowser.PeekQueueDeadLetterAsync(_connectionString!, queueName, maxMessages, fromSequenceNumber, cancellationToken)
                : await MessageBrowser.PeekQueueAsync(_connectionString!, queueName, maxMessages, fromSequenceNumber, cancellationToken);
        }
        if (TryGetSubscription(out var topicName, out var subscriptionName))
        {
            return isDeadLetter
                ? await MessageBrowser.PeekSubscriptionDeadLetterAsync(_connectionString!, topicName, subscriptionName, maxMessages, fromSequenceNumber, cancellationToken)
                : await MessageBrowser.PeekSubscriptionAsync(_connectionString!, topicName, subscriptionName, maxMessages, fromSequenceNumber, cancellationToken);
        }
        return Array.Empty<MessagePreview>();
    }

    private static long? GetNextAnchor(long? currentAnchor, IReadOnlyList<MessagePreview> items)
    {
        if (items.Count == 0) return currentAnchor;
        var maxSeq = items.Max(m => m.SequenceNumber);
        return maxSeq == long.MaxValue ? long.MaxValue : maxSeq + 1;
    }

    private CancellationToken StartSendCancellation()
    {
        StopSend();
        _sendCts = new CancellationTokenSource();
        return _sendCts.Token;
    }

    private void StopSend()
    {
        try { _sendCts?.Cancel(); } catch { }
        try { _sendCts?.Dispose(); } catch { }
        _sendCts = null;
    }

    // Active/DLQ refresh and live polling
    private async Task RefreshActiveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRefreshMessages || _refreshingActive) return;
        if (cancellationToken.IsCancellationRequested) return;
        try
        {
            _refreshingActive = true;
            IReadOnlyList<MessagePreview> items = Array.Empty<MessagePreview>();
            items = await PeekSelectedMessagesAsync(isDeadLetter: false, _activeAnchor, FetchSize, cancellationToken);
            // If not in live mode and our anchor points past the end (yielding no items), reset to null and refetch once
            if (!_liveActive && _activeAnchor.HasValue && items.Count == 0)
            {
                _activeAnchor = null;
                items = await PeekSelectedMessagesAsync(isDeadLetter: false, _activeAnchor, FetchSize, cancellationToken);
            }
            if (_liveActive && _pendingActiveClear)
            {
                _activeMessages.Clear();
                _activeHistory.Clear();
                _activeAnchor = null;
                _pendingActiveClear = false;
            }
            if (_liveActive)
            {
                _activeAnchor = GetNextAnchor(_activeAnchor, items);
                _activeMessages = MergeLatestWindow(_activeMessages, items, FetchSize);
                ReconcileActiveFromDlq();
            }
            else
            {
                _activeMessages = items
                    .OrderBy(m => m.EnqueuedTime)
                    .ToList();
                ReconcileActiveFromDlq();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to refresh active messages: {ex.Message}", Severity.Error);
        }
        finally
        {
            _refreshingActive = false;
        }
    }

    private async Task RefreshDlqAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRefreshMessages || _refreshingDlq) return;
        if (cancellationToken.IsCancellationRequested) return;
        try
        {
            _refreshingDlq = true;
            if (_liveDlq)
            {
                IReadOnlyList<MessagePreview> liveItems = await PeekSelectedMessagesAsync(isDeadLetter: true, _dlqAnchor, FetchSize, cancellationToken);
                if (_pendingDlqClear)
                {
                    _dlqMessages.Clear();
                    _dlqHistory.Clear();
                    _dlqAnchor = null;
                    _pendingDlqClear = false;
                }
                _dlqAnchor = GetNextAnchor(_dlqAnchor, liveItems);
                _dlqMessages = MergeLatestWindow(_dlqMessages, liveItems, FetchSize);
                ReconcileActiveFromDlq();
            }
            else
            {
                var collected = new List<MessagePreview>();
                var anchor = _dlqAnchor;
                var target = _dlqCount.HasValue ? (int)Math.Min(_dlqCount.Value, FetchSize * 10) : FetchSize;
                while (collected.Count < target && !cancellationToken.IsCancellationRequested)
                {
                    var page = await PeekSelectedMessagesAsync(isDeadLetter: true, anchor, FetchSize, cancellationToken);
                    if (page.Count == 0) break;
                    collected.AddRange(page);
                    anchor = GetNextAnchor(anchor, page);
                    if (page.Count < FetchSize) break;
                }
                _dlqAnchor = anchor;
                _dlqMessages = collected
                    .OrderBy(m => m.EnqueuedTime)
                    .ToList();
                ReconcileActiveFromDlq();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to refresh DLQ messages: {ex.Message}", Severity.Error);
        }
        finally
        {
            _refreshingDlq = false;
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
                try { await InvokeAsync(StateHasChanged); } catch { }
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
                try { await InvokeAsync(StateHasChanged); } catch { }
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
                _activeAnchor = null;
                _activeHistory.Clear();
            }
            if ((_dlqCount ?? 0) == 0)
            {
                _dlqMessages.Clear();
                _dlqAnchor = null;
                _dlqHistory.Clear();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to refresh counts: {ex.Message}", Severity.Error);
        }
        finally
        {
            if (!_disposed)
            {
                try { await InvokeAsync(StateHasChanged); } catch { }
            }
        }
    }

    private async Task OnActiveRefresh()
    {
        if (!CanRefreshMessages) return;
        if (!_liveActive)
        {
            _activeAnchor = null;
            _activeHistory.Clear();
        }
        await RefreshCountsAsync();
        await RefreshActiveAsync();
        if (!_disposed)
        {
            try { await InvokeAsync(StateHasChanged); } catch { }
        }
    }

    private async Task OnDlqRefresh()
    {
        if (!CanRefreshMessages) return;
        if (!_liveDlq)
        {
            _dlqAnchor = null;
            _dlqHistory.Clear();
        }
        await RefreshCountsAsync();
        await RefreshDlqAsync();
        if (!_disposed)
        {
            try { await InvokeAsync(StateHasChanged); } catch { }
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
        // Reset paging to latest when starting live
        _activeAnchor = (_activeMessages is { Count: > 0 })
            ? (_activeMessages.Max(m => m.SequenceNumber) == long.MaxValue ? long.MaxValue : _activeMessages.Max(m => m.SequenceNumber) + 1)
            : null;
        _activeHistory.Clear();
        _liveActiveCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            var token = _liveActiveCts.Token;
            while (!token.IsCancellationRequested && !_disposed)
            {
                await RefreshActiveAsync(token);
                if (_disposed || token.IsCancellationRequested) break;
                try { await InvokeAsync(StateHasChanged); } catch { }
                try { await Task.Delay(1000, token); } catch { break; }
            }
        });
    }

    private void StopLiveActive()
    {
        _liveActive = false;
        try { _liveActiveCts?.Cancel(); } catch { }
        try { _liveActiveCts?.Dispose(); } catch { }
        _liveActiveCts = null;
    }

    private void StartLiveDlq()
    {
        if (!CanRefreshMessages || _disposed) return;
        StopLiveDlq();
        _liveDlq = true;
        // Reset paging to latest when starting live
        _dlqAnchor = (_dlqMessages is { Count: > 0 })
            ? (_dlqMessages.Max(m => m.SequenceNumber) == long.MaxValue ? long.MaxValue : _dlqMessages.Max(m => m.SequenceNumber) + 1)
            : null;
        _dlqHistory.Clear();
        _liveDlqCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            var token = _liveDlqCts.Token;
            while (!token.IsCancellationRequested && !_disposed)
            {
                await RefreshDlqAsync(token);
                if (_disposed || token.IsCancellationRequested) break;
                try { await InvokeAsync(StateHasChanged); } catch { }
                try { await Task.Delay(1000, token); } catch { break; }
            }
        });
    }

    private void StopLiveDlq()
    {
        _liveDlq = false;
        try { _liveDlqCts?.Cancel(); } catch { }
        try { _liveDlqCts?.Dispose(); } catch { }
        _liveDlqCts = null;
    }

    private void StartCountsPolling()
    {
        if (string.IsNullOrEmpty(_selectedTreeValue) || _disposed) return;
        StopCountsPolling();
        _countsCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            var token = _countsCts!.Token;
            while (!token.IsCancellationRequested && !_disposed)
            {
                await RefreshCountsAsync(token);
                try { await Task.Delay(CountsRefreshIntervalMs, token); } catch { break; }
            }
        });
    }

    private void StopCountsPolling()
    {
        try { _countsCts?.Cancel(); } catch { }
        try { _countsCts?.Dispose(); } catch { }
        _countsCts = null;
    }

    // Merge helper: dedupe by sequence, keep most recent by EnqueuedTime, limit to window size
    private static List<MessagePreview> MergeLatestWindow(List<MessagePreview> existing, IReadOnlyList<MessagePreview> incoming, int windowSize)
    {
        if (incoming is null || incoming.Count == 0)
            return existing ?? new List<MessagePreview>();

        var map = (existing ?? new List<MessagePreview>())
            .ToDictionary(m => m.SequenceNumber, m => m);

        foreach (var msg in incoming)
            map[msg.SequenceNumber] = msg;

        // Keep the latest windowSize items by EnqueuedTime, but display ascending in the UI
        return map.Values
            .OrderByDescending(m => m.EnqueuedTime)
            .Take(windowSize)
            .OrderBy(m => m.EnqueuedTime)
            .ToList();
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
    }
}
