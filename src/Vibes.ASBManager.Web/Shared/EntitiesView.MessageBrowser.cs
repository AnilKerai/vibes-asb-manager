using Microsoft.Extensions.Logging;
using MudBlazor;
using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Web.Shared;

// Thin adapter between EntitiesView and the MessageBrowsingController, which owns the
// browse/live/counts/purge state. The properties below keep the names the markup binds to; the
// methods forward to the controller and re-render. Dialogs stay here because the controller is
// UI-free (it raises StateChanged / Notify instead of touching the renderer or snackbar).
public partial class EntitiesView
{
    private MessageBrowsingController _messages = default!;
    private CancellationTokenSource? _sendCts;
    private volatile bool _disposed; // set on Dispose; read by the background send loop

    protected override void OnInitialized()
    {
        _messages = new MessageBrowsingController(MessageBrowser, MessageMaintenance, DeadLetterMaintenance, QueueAdmin, SubscriptionAdmin, Logger);
        _messages.StateChanged += OnMessagesStateChanged;
        _messages.Notify += OnMessagesNotify;
    }

    private void OnMessagesStateChanged() => _ = InvokeAsync(StateHasChanged);
    private void OnMessagesNotify(string message, Severity severity) => Snackbar.Add(message, severity);

    // State the markup binds to, delegated to the controller.
    private IReadOnlyList<MessagePreview> _activeMessages => _messages.ActiveMessages;
    private IReadOnlyList<MessagePreview> _dlqMessages => _messages.DlqMessages;
    private long? _activeCount => _messages.ActiveCount;
    private long? _dlqCount => _messages.DlqCount;
    private bool _liveActive => _messages.LiveActive;
    private bool _liveDlq => _messages.LiveDlq;
    private bool _purgingActive => _messages.PurgingActive;
    private bool _purgingDlq => _messages.PurgingDlq;
    private int _purgeProgress => _messages.PurgeProgress;

    // Translate the current tree selection into the controller's target.
    private MessageTarget BuildMessageTarget()
    {
        if (TryGetQueue(out var queue)) return new MessageTarget(_connectionString, queue, null, null);
        if (TryGetSubscription(out var topic, out var sub)) return new MessageTarget(_connectionString, null, topic, sub);
        return new MessageTarget(_connectionString, null, null, null);
    }

    private async Task OnActiveRefresh() { await _messages.OnActiveRefresh(); await InvokeAsync(StateHasChanged); }
    private async Task OnDlqRefresh() { await _messages.OnDlqRefresh(); await InvokeAsync(StateHasChanged); }

    private Task ToggleLiveBothAsync()
    {
        _messages.ToggleLiveBoth();
        return InvokeAsync(StateHasChanged);
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
        catch (Exception ex) { Logger?.LogDebug(ex, "Failed to cancel send operation."); }
        try { _sendCts?.Dispose(); }
        catch (Exception ex) { Logger?.LogDebug(ex, "Failed to dispose send cancellation token."); }
        _sendCts = null;
    }

    private async Task OnActiveRowClick(TableRowClickEventArgs<MessagePreview> args)
    {
        if (args?.Item is null) return;
        await ShowMessageDetailsAsync(args.Item.SequenceNumber, isDeadLetter: false, _messages.ActiveMessages.ToList());
    }

    private async Task OnDlqRowClick((MessagePreview Item, IReadOnlyList<MessagePreview> Visible) args)
    {
        if (args.Item is null) return;
        await ShowMessageDetailsAsync(args.Item.SequenceNumber, isDeadLetter: true, args.Visible);
    }

    // Peek a single message's full details for the current tree selection. Shared by the initial
    // open and by prev/next navigation inside the dialog.
    private Task<MessageDetails?> PeekDetailsAsync(long sequenceNumber, bool isDeadLetter)
    {
        if (string.IsNullOrWhiteSpace(_connectionString)) return Task.FromResult<MessageDetails?>(null);
        if (TryGetQueue(out var q))
        {
            return isDeadLetter
                ? MessageBrowser.PeekQueueDeadLetterMessageAsync(_connectionString!, q, sequenceNumber)
                : MessageBrowser.PeekQueueMessageAsync(_connectionString!, q, sequenceNumber);
        }
        if (TryGetSubscription(out var t, out var s))
        {
            return isDeadLetter
                ? MessageBrowser.PeekSubscriptionDeadLetterMessageAsync(_connectionString!, t, s, sequenceNumber)
                : MessageBrowser.PeekSubscriptionMessageAsync(_connectionString!, t, s, sequenceNumber);
        }
        return Task.FromResult<MessageDetails?>(null);
    }

    private async Task ShowMessageDetailsAsync(long sequenceNumber, bool isDeadLetter, IReadOnlyList<MessagePreview> previews)
    {
        if (string.IsNullOrWhiteSpace(_connectionString)) return;
        try
        {
            var details = await PeekDetailsAsync(sequenceNumber, isDeadLetter);

            if (details is null)
            {
                Snackbar.Add("Message not found. It may have moved or expired.", Severity.Info);
                return;
            }

            var parameters = new DialogParameters
            {
                ["Details"] = details,
                ["IsDeadLetter"] = isDeadLetter,
                ["ConnectionString"] = _connectionString!,
                ["Previews"] = previews,
                ["LoadDetails"] = (Func<long, Task<MessageDetails?>>)(seq => PeekDetailsAsync(seq, isDeadLetter))
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
                // A resubmit from the dialog can change both lists.
                await _messages.RefreshAfterReplayAsync();
                await InvokeAsync(StateHasChanged);
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
        _messages?.Dispose();
        StopSend();
    }
}
