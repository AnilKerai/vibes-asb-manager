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
}
