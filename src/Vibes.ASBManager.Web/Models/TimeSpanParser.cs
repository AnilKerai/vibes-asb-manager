using System.Globalization;

namespace Vibes.ASBManager.Web.Models;

public static class TimeSpanParser
{
    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out value)
            || TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out value);
    }

    // Render a TimeSpan as the invariant constant ("c") form (e.g. "1.00:00:00"), which TryParse reads
    // back. Used to populate the settings text fields from an entity's properties.
    public static string Format(TimeSpan value) => value.ToString("c", CultureInfo.InvariantCulture);
}
