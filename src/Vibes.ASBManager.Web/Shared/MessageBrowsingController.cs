using Microsoft.Extensions.Logging;
using MudBlazor;
using Vibes.ASBManager.Application.Interfaces.Admin;
using Vibes.ASBManager.Application.Interfaces.Messaging;
using Vibes.ASBManager.Application.Messaging;
using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Web.Shared;

// The entity a MessageBrowsingController is pointed at: a queue, or a topic subscription, on a
// given connection. Empty/none when nothing browsable is selected.
public readonly record struct MessageTarget(string? ConnectionString, string? QueueName, string? TopicName, string? SubscriptionName)
{
    public static readonly MessageTarget None = new(null, null, null, null);
    public bool IsQueue => !string.IsNullOrWhiteSpace(QueueName);
    public bool IsSubscription => !string.IsNullOrWhiteSpace(TopicName) && !string.IsNullOrWhiteSpace(SubscriptionName);
    public bool CanBrowse => !string.IsNullOrWhiteSpace(ConnectionString) && (IsQueue || IsSubscription);
}

// Owns the "browse the selected entity's messages" concern that used to live inline in EntitiesView:
// the active/DLQ snapshots, runtime counts, live polling, and purge. It's UI-agnostic — it raises
// StateChanged when the host should re-render and Notify when a toast should show, so the pure logic
// is testable and the component stays thin. Dialogs (purge confirm, message details) stay in the host.
public sealed class MessageBrowsingController : IDisposable
{
    private const int FetchSize = 50;                  // messages peeked per API call
    private const int MaxSnapshotMessages = FetchSize * 10; // cap a single snapshot at 500
    private const int MaxEmptyPeeks = 3;               // tolerate transient empty peek batches
    private const int CountsRefreshIntervalMs = 2000;
    private const int LiveRefreshIntervalMs = 2000;

    private readonly IMessageBrowser _browser;
    private readonly IMessageMaintenance _maintenance;
    private readonly IDeadLetterMaintenance _deadLetter;
    private readonly IQueueAdmin _queueAdmin;
    private readonly ISubscriptionAdmin _subscriptionAdmin;
    private readonly ILogger? _logger;

    public MessageBrowsingController(
        IMessageBrowser browser,
        IMessageMaintenance maintenance,
        IDeadLetterMaintenance deadLetter,
        IQueueAdmin queueAdmin,
        ISubscriptionAdmin subscriptionAdmin,
        ILogger? logger = null)
    {
        _browser = browser;
        _maintenance = maintenance;
        _deadLetter = deadLetter;
        _queueAdmin = queueAdmin;
        _subscriptionAdmin = subscriptionAdmin;
        _logger = logger;
    }

    // Raised when the host component should re-render. Raised when a toast should be shown.
    public event Action? StateChanged;
    public event Action<string, Severity>? Notify;

    private MessageTarget _target = MessageTarget.None;

    private List<MessagePreview> _activeMessages = new();
    private List<MessagePreview> _dlqMessages = new();
    private int _refreshingActive; // 0 = idle, 1 = in flight (Interlocked: poll loop vs UI action)
    private int _refreshingDlq;
    private string? _activeRefreshError; // last surfaced error per op; null = healthy (C3 dedup)
    private string? _dlqRefreshError;
    private string? _countsRefreshError;

    private bool _liveActive;
    private bool _liveDlq;
    private CancellationTokenSource? _liveActiveCts;
    private CancellationTokenSource? _liveDlqCts;
    private bool _pendingActiveClear;
    private bool _pendingDlqClear;
    private long? _activeCount;
    private long? _dlqCount;
    private CancellationTokenSource? _countsCts;

    private bool _purgingActive;
    private bool _purgingDlq;
    private int _purgeProgress;
    private CancellationTokenSource? _purgeCts;
    private volatile bool _disposed;

    // --- state the host reads (action bar bindings, tables, chips) ---
    public IReadOnlyList<MessagePreview> ActiveMessages => _activeMessages;
    public IReadOnlyList<MessagePreview> DlqMessages => _dlqMessages;
    public long? ActiveCount => _activeCount;
    public long? DlqCount => _dlqCount;
    public bool LiveActive => _liveActive;
    public bool LiveDlq => _liveDlq;
    public bool LiveOn => _liveActive || _liveDlq;
    public bool PurgingActive => _purgingActive;
    public bool PurgingDlq => _purgingDlq;
    public int PurgeProgress => _purgeProgress;
    private bool CanRefreshMessages => _target.CanBrowse && !_disposed;

    // ----- pure, unit-tested helpers -----

    // Snapshot target: clamp a known runtime count to [0, max]; fall back to fetchSize when unknown.
    public static int SnapshotTarget(long? knownCount, int fetchSize, int maxSnapshot)
        => knownCount.HasValue ? (int)Math.Clamp(knownCount.Value, 0, maxSnapshot) : fetchSize;

    // Active minus anything now present in the DLQ (matched by sequence number).
    public static List<MessagePreview> ReconcileActiveFromDlq(IReadOnlyList<MessagePreview> active, IReadOnlyList<MessagePreview> dlq)
    {
        if (active.Count == 0 || dlq.Count == 0) return active.ToList();
        var dlqSeq = dlq.Select(m => m.SequenceNumber).ToHashSet();
        return active.Where(m => !dlqSeq.Contains(m.SequenceNumber)).ToList();
    }

    // Error-toast de-duplication: returns whether to toast and the new "last error" to remember.
    public static (bool ShouldToast, string? LastError) NotifyOutcome(string? lastError, string? message)
    {
        if (message is null) return (false, null);                 // success resets the tracked error
        if (string.Equals(message, lastError, StringComparison.Ordinal)) return (false, lastError);
        return (true, message);
    }

    // Toast text after a purge — ceiling-aware ("more may remain" when the safety cap was hit).
    public static string PurgeResultMessage(int purged, string entity, bool isDeadLetter)
    {
        var what = isDeadLetter ? "DLQ messages" : "messages";
        return purged >= MessagingDefaults.PurgeCeiling
            ? $"Purged {purged:N0} {what} from {entity} (ceiling reached — more may remain; purge again to continue)."
            : $"Purged {purged:N0} {what} from {entity}.";
    }

    private void RaiseStateChanged()
    {
        if (!_disposed) StateChanged?.Invoke();
    }

    private string? ApplyOutcome(string? lastError, string? message)
    {
        var (shouldToast, newLastError) = NotifyOutcome(lastError, message);
        if (shouldToast && message is not null) Notify?.Invoke(message, Severity.Error);
        return newLastError;
    }

    // ----- target lifecycle -----

    // Point at a new entity: stop polling, clear, then load counts + lists and resume counts polling.
    // Mirrors the old OnTreeSelectionChanged message sequence exactly.
    public async Task SetTargetAsync(MessageTarget target)
    {
        _target = target;
        StopLiveActive();
        StopLiveDlq();
        StopCountsPolling();
        _activeMessages.Clear();
        _dlqMessages.Clear();
        await RefreshCountsAsync();
        StartCountsPolling();
        await RefreshActiveAsync();
        await RefreshDlqAsync();
        RaiseStateChanged();
    }

    // Clear everything (e.g. on connection switch); no selection.
    public void Reset()
    {
        _target = MessageTarget.None;
        StopLiveActive();
        StopLiveDlq();
        StopCountsPolling();
        _activeMessages.Clear();
        _dlqMessages.Clear();
        _activeCount = null;
        _dlqCount = null;
        _activeRefreshError = null;
        _dlqRefreshError = null;
        _countsRefreshError = null;
    }

    // ----- snapshots / refresh -----

    private async Task<List<MessagePreview>> PeekSnapshotAsync(bool isDeadLetter, long? knownCount, CancellationToken cancellationToken)
    {
        var target = SnapshotTarget(knownCount, FetchSize, MaxSnapshotMessages);
        if (target <= 0) return new List<MessagePreview>();

        var connectionString = _target.ConnectionString!;
        IReadOnlyList<MessagePreview> snapshot;
        if (_target.IsQueue)
        {
            snapshot = isDeadLetter
                ? await _browser.PeekQueueDeadLetterSnapshotAsync(connectionString, _target.QueueName!, target, FetchSize, MaxEmptyPeeks, cancellationToken)
                : await _browser.PeekQueueSnapshotAsync(connectionString, _target.QueueName!, target, FetchSize, MaxEmptyPeeks, cancellationToken);
        }
        else if (_target.IsSubscription)
        {
            snapshot = isDeadLetter
                ? await _browser.PeekSubscriptionDeadLetterSnapshotAsync(connectionString, _target.TopicName!, _target.SubscriptionName!, target, FetchSize, MaxEmptyPeeks, cancellationToken)
                : await _browser.PeekSubscriptionSnapshotAsync(connectionString, _target.TopicName!, _target.SubscriptionName!, target, FetchSize, MaxEmptyPeeks, cancellationToken);
        }
        else
        {
            return new List<MessagePreview>();
        }
        return snapshot.ToList();
    }

    public async Task RefreshActiveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRefreshMessages || cancellationToken.IsCancellationRequested) return;
        if (Interlocked.CompareExchange(ref _refreshingActive, 1, 0) != 0) return;
        try
        {
            if (_pendingActiveClear)
            {
                _activeMessages.Clear();
                _pendingActiveClear = false;
            }
            var snapshot = await PeekSnapshotAsync(isDeadLetter: false, _activeCount, cancellationToken);
            _activeMessages = snapshot.OrderBy(m => m.EnqueuedTime).ToList();
            _activeMessages = ReconcileActiveFromDlq(_activeMessages, _dlqMessages);
            _activeRefreshError = ApplyOutcome(_activeRefreshError, null);
        }
        catch (Exception ex)
        {
            _activeRefreshError = ApplyOutcome(_activeRefreshError, $"Failed to refresh active messages: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshingActive, 0);
        }
    }

    public async Task RefreshDlqAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRefreshMessages || cancellationToken.IsCancellationRequested) return;
        if (Interlocked.CompareExchange(ref _refreshingDlq, 1, 0) != 0) return;
        try
        {
            if (_pendingDlqClear)
            {
                _dlqMessages.Clear();
                _pendingDlqClear = false;
            }
            var snapshot = await PeekSnapshotAsync(isDeadLetter: true, _dlqCount, cancellationToken);
            _dlqMessages = snapshot.OrderBy(m => m.EnqueuedTime).ToList();
            _activeMessages = ReconcileActiveFromDlq(_activeMessages, _dlqMessages);
            _dlqRefreshError = ApplyOutcome(_dlqRefreshError, null);
        }
        catch (Exception ex)
        {
            _dlqRefreshError = ApplyOutcome(_dlqRefreshError, $"Failed to refresh DLQ messages: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshingDlq, 0);
        }
    }

    private async Task RefreshActiveIfNeededAsync()
    {
        if (CanRefreshMessages && _activeMessages.Count > 0)
        {
            await RefreshActiveAsync();
            RaiseStateChanged();
        }
    }

    private async Task RefreshDlqIfNeededAsync()
    {
        if (CanRefreshMessages && _dlqMessages.Count > 0)
        {
            await RefreshDlqAsync();
            RaiseStateChanged();
        }
    }

    public async Task RefreshCountsAsync(CancellationToken cancellationToken = default)
    {
        if (!_target.CanBrowse) { _activeCount = null; _dlqCount = null; return; }
        if (cancellationToken.IsCancellationRequested) return;
        try
        {
            if (_target.IsQueue)
            {
                var qs = await _queueAdmin.GetQueueRuntimeAsync(_target.ConnectionString!, _target.QueueName!, cancellationToken);
                _activeCount = qs?.ActiveMessageCount;
                _dlqCount = qs?.DeadLetterMessageCount;
            }
            else if (_target.IsSubscription)
            {
                var ss = await _subscriptionAdmin.GetSubscriptionRuntimeAsync(_target.ConnectionString!, _target.TopicName!, _target.SubscriptionName!, cancellationToken);
                _activeCount = ss?.ActiveMessageCount;
                _dlqCount = ss?.DeadLetterMessageCount;
            }
            else
            {
                _activeCount = null;
                _dlqCount = null;
            }

            if ((_activeCount ?? 0) == 0) _activeMessages.Clear();
            if ((_dlqCount ?? 0) == 0) _dlqMessages.Clear();
            _countsRefreshError = ApplyOutcome(_countsRefreshError, null);
        }
        catch (Exception ex)
        {
            _countsRefreshError = ApplyOutcome(_countsRefreshError, $"Failed to refresh counts: {ex.Message}");
        }
        finally
        {
            RaiseStateChanged();
        }
    }

    public async Task OnActiveRefresh()
    {
        if (!CanRefreshMessages) return;
        await RefreshCountsAsync();
        await RefreshActiveAsync();
        RaiseStateChanged();
    }

    public async Task OnDlqRefresh()
    {
        if (!CanRefreshMessages) return;
        await RefreshCountsAsync();
        await RefreshDlqAsync();
        RaiseStateChanged();
    }

    public async Task RefreshAfterReplayAsync()
    {
        await RefreshActiveIfNeededAsync();
        await RefreshDlqIfNeededAsync();
        await RefreshCountsAsync();
    }

    // ----- live polling -----

    // Runs tickAsync immediately, then on a fixed cadence, until the token is cancelled or disposed.
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
        RaiseStateChanged();
    }

    private async Task RefreshAndRenderDlqAsync(CancellationToken token)
    {
        await RefreshDlqAsync(token);
        if (_disposed || token.IsCancellationRequested) return;
        RaiseStateChanged();
    }

    public void ToggleLiveBoth()
    {
        if (!LiveOn)
        {
            StartLiveActive();
            StartLiveDlq();
        }
        else
        {
            StopLiveActive();
            StopLiveDlq();
        }
    }

    private void StartLiveActive()
    {
        if (!CanRefreshMessages) return;
        StopLiveActive();
        _liveActive = true;
        _liveActiveCts = new CancellationTokenSource();
        RunPollLoop(_liveActiveCts.Token, LiveRefreshIntervalMs, RefreshAndRenderActiveAsync);
    }

    private void StopLiveActive()
    {
        _liveActive = false;
        TryCancelDispose(ref _liveActiveCts, "live active polling");
    }

    private void StartLiveDlq()
    {
        if (!CanRefreshMessages) return;
        StopLiveDlq();
        _liveDlq = true;
        _liveDlqCts = new CancellationTokenSource();
        RunPollLoop(_liveDlqCts.Token, LiveRefreshIntervalMs, RefreshAndRenderDlqAsync);
    }

    private void StopLiveDlq()
    {
        _liveDlq = false;
        TryCancelDispose(ref _liveDlqCts, "live DLQ polling");
    }

    private void StartCountsPolling()
    {
        if (!_target.CanBrowse || _disposed) return;
        StopCountsPolling();
        _countsCts = new CancellationTokenSource();
        RunPollLoop(_countsCts.Token, CountsRefreshIntervalMs, RefreshCountsAsync);
    }

    private void StopCountsPolling() => TryCancelDispose(ref _countsCts, "counts polling");

    private void TryCancelDispose(ref CancellationTokenSource? cts, string what)
    {
        try { cts?.Cancel(); }
        catch (Exception ex) { _logger?.LogDebug(ex, "Failed to cancel {What}.", what); }
        try { cts?.Dispose(); }
        catch (Exception ex) { _logger?.LogDebug(ex, "Failed to dispose {What} token.", what); }
        cts = null;
    }

    // ----- purge -----

    public Task PurgeActiveAsync() => RunPurgeAsync(isDeadLetter: false);
    public Task PurgeDlqAsync() => RunPurgeAsync(isDeadLetter: true);

    private async Task RunPurgeAsync(bool isDeadLetter)
    {
        if (!_target.CanBrowse) return;
        var entityLabel = _target.IsQueue ? _target.QueueName! : $"{_target.TopicName}/{_target.SubscriptionName}";

        if (isDeadLetter) _purgingDlq = true; else _purgingActive = true;
        _purgeProgress = 0;
        RaiseStateChanged();

        StopPurge();
        _purgeCts = new CancellationTokenSource();
        var token = _purgeCts.Token;
        var progress = new Progress<int>(n => { _purgeProgress = n; RaiseStateChanged(); });
        try
        {
            var purged = await PurgeAsync(isDeadLetter, progress, token);
            Notify?.Invoke(PurgeResultMessage(purged, entityLabel, isDeadLetter), Severity.Success);
            if (isDeadLetter) await ClearDlqAfterPurgeAsync(); else await ClearActiveAfterPurgeAsync();
        }
        catch (OperationCanceledException)
        {
            if (!_disposed) Notify?.Invoke($"Purge of {entityLabel} cancelled after {_purgeProgress:N0} messages.", Severity.Info);
        }
        catch (Exception ex)
        {
            Notify?.Invoke($"Failed to purge {entityLabel}: {ex.Message}", Severity.Error);
        }
        finally
        {
            if (isDeadLetter) _purgingDlq = false; else _purgingActive = false;
            StopPurge();
            RaiseStateChanged();
        }
    }

    private Task<int> PurgeAsync(bool isDeadLetter, IProgress<int> progress, CancellationToken token)
    {
        var connectionString = _target.ConnectionString!;
        if (_target.IsQueue)
        {
            return isDeadLetter
                ? _deadLetter.PurgeQueueDeadLetterAsync(connectionString, _target.QueueName!, progress: progress, cancellationToken: token)
                : _maintenance.PurgeQueueAsync(connectionString, _target.QueueName!, progress: progress, cancellationToken: token);
        }
        return isDeadLetter
            ? _deadLetter.PurgeSubscriptionDeadLetterAsync(connectionString, _target.TopicName!, _target.SubscriptionName!, progress: progress, cancellationToken: token)
            : _maintenance.PurgeSubscriptionAsync(connectionString, _target.TopicName!, _target.SubscriptionName!, progress: progress, cancellationToken: token);
    }

    private async Task ClearActiveAfterPurgeAsync()
    {
        _pendingActiveClear = true;
        _activeMessages.Clear();
        RaiseStateChanged();
        await RefreshActiveIfNeededAsync();
        await RefreshCountsAsync();
    }

    private async Task ClearDlqAfterPurgeAsync()
    {
        _pendingDlqClear = true;
        _dlqMessages.Clear();
        RaiseStateChanged();
        await RefreshDlqIfNeededAsync();
        await RefreshCountsAsync();
    }

    private void StopPurge() => TryCancelDispose(ref _purgeCts, "purge operation");

    public void Dispose()
    {
        _disposed = true;
        StopLiveActive();
        StopLiveDlq();
        StopCountsPolling();
        StopPurge();
    }
}
