using MudBlazor;

namespace Vibes.ASBManager.Web.Shared;

public partial class EntitiesActionBar
{
    private bool RefreshDisabled => !CanLoad;
    private bool LiveDisabled => !CanRefreshMessages;
    private Color LiveButtonColor => LiveOn ? Color.Success : Color.Default;
    private string LiveButtonIcon => LiveOn ? Icons.Material.Filled.Pause : Icons.Material.Filled.PlayArrow;
    private string LiveButtonText => LiveOn ? "Live On" : "Live";

    private bool SendDisabled => !CanSend;
    private bool PurgeActiveDisabled => !CanPurgeActive;
    private bool ResubmitDlqDisabled => !CanResubmitDlq;
    private bool PurgeDlqDisabled => !CanPurgeDlq;

    private bool ShowCreateQueue => HasConnection && (SelectedIsRoot || SelectedIsQueuesFolder);
    private bool ShowCreateTopic => HasConnection && (SelectedIsRoot || SelectedIsTopicsFolder);
    private bool ShowDeleteQueue => SelectedIsQueue;
    private bool ShowDeleteTopic => SelectedIsTopic;
    private bool ShowCreateSubscription => SelectedIsTopic;
    private bool ShowDeleteSubscription => SelectedIsSubscription;

    private bool CreateSubscriptionDisabled => !CanCreateSubscriptionAction;
    private bool DeleteSubscriptionDisabled => !CanDeleteSubscriptionAction;
}
