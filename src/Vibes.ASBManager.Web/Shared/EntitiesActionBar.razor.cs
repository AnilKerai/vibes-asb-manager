using MudBlazor;

namespace Vibes.ASBManager.Web.Shared;

public partial class EntitiesActionBar
{
    private bool RefreshDisabled => !CanLoad;
    private bool LiveDisabled => !CanRefreshMessages;
    private Color LiveButtonColor => LiveOn ? Color.Success : Color.Default;
    private string LiveButtonIcon => LiveOn ? Icons.Material.Filled.Pause : Icons.Material.Filled.PlayArrow;
    private string LiveButtonText => LiveOn ? "Live On" : "Live";

    private bool PurgeBusy => PurgingActive || PurgingDlq;
    private bool SendDisabled => !CanSend || PurgeBusy;
    private bool PurgeActiveDisabled => !CanPurgeActive || PurgeBusy;
    private bool ResubmitDlqDisabled => !CanResubmitDlq || PurgeBusy;
    private bool PurgeDlqDisabled => !CanPurgeDlq || PurgeBusy;

    private bool ShowCreateQueue => HasConnection && (SelectedIsRoot || SelectedIsQueuesFolder);
    private bool ShowCreateTopic => HasConnection && (SelectedIsRoot || SelectedIsTopicsFolder);
    private bool ShowDeleteQueue => SelectedIsQueue;
    private bool ShowDeleteTopic => SelectedIsTopic;
    private bool ShowCreateSubscription => SelectedIsTopic;
    private bool ShowDeleteSubscription => SelectedIsSubscription;

    private bool CreateSubscriptionDisabled => !CanCreateSubscriptionAction;
    private bool DeleteSubscriptionDisabled => !CanDeleteSubscriptionAction;
}
