namespace Vibes.ASBManager.Web.Models;

// Normalises raw settings-panel input before it's sent to the admin layer. Kept pure (and out of the
// component) so the rules are unit-tested in one place rather than duplicated inline per entity type.
public static class SettingsSanitizer
{
    // Service Bus requires MaxDeliveryCount >= 1; floor whatever the UI supplied.
    public static int MaxDeliveryCount(int value) => Math.Max(1, value);

    // A blank forward address means "none" (clear it); otherwise trim surrounding whitespace.
    public static string? ForwardAddress(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
