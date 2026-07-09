using Microsoft.AspNetCore.Components;
using MudBlazor;
using Vibes.ASBManager.Application.Messaging;
using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Web.Shared;

// Shared behaviour for the active and dead-letter message tables: the live search box's filtering,
// the "showing N of M" counts, and handing the currently-visible (filtered) set to the details dialog
// on row click so its prev/next navigation stays within the search results. Each table supplies its
// own markup — title, column labels, and the one column that differs (Subject vs DLQ Reason).
public abstract class MessageTableBase : ComponentBase
{
    [Parameter] public IReadOnlyList<MessagePreview>? Items { get; set; }
    [Parameter] public bool CanRefresh { get; set; }
    [Parameter] public EventCallback OnRefresh { get; set; }

    // Carries the clicked message plus the currently-visible (filtered) set, so the details dialog can
    // navigate prev/next within the search results rather than the whole loaded snapshot.
    [Parameter] public EventCallback<(MessagePreview Item, IReadOnlyList<MessagePreview> Visible)> OnRowClick { get; set; }
    [Parameter] public long? Count { get; set; }

    protected string? Search;

    protected int LoadedCount => Items?.Count ?? 0;

    protected IReadOnlyList<MessagePreview> FilteredItems
        => string.IsNullOrWhiteSpace(Search) || Items is null
            ? Items ?? Array.Empty<MessagePreview>()
            : Items.Where(m => MessageSearch.Matches(m, Search)).ToList();

    // Title suffix: matched/loaded while searching, else the runtime count.
    protected string CountSuffix
        => !string.IsNullOrWhiteSpace(Search)
            ? $"(showing {FilteredItems.Count} of {LoadedCount})"
            : Count.HasValue ? $"({Count.Value})" : string.Empty;

    // Shown only when a search is active and the loaded snapshot didn't cover the whole entity.
    protected bool ShowScopeHint
        => !string.IsNullOrWhiteSpace(Search) && Count.HasValue && LoadedCount < Count.Value;

    protected string NoRecordsText(string emptyLabel)
        => string.IsNullOrWhiteSpace(Search) ? emptyLabel : "No messages match your search.";

    protected async Task HandleRowClick(TableRowClickEventArgs<MessagePreview> args)
    {
        if (args?.Item is null) return;
        await OnRowClick.InvokeAsync((args.Item, FilteredItems));
    }
}
