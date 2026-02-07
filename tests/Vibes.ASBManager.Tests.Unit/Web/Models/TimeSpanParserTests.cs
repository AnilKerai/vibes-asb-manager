using Vibes.ASBManager.Web.Models;

namespace Vibes.ASBManager.Tests.Unit.Web.Models;

public class TimeSpanParserTests
{
    [Theory]
    [InlineData("01:02:03", 1, 2, 3)]
    [InlineData("1.02:03:04", 26, 3, 4)]
    public void TryParse_ValidTimeSpans_ReturnsTrue(string input, int totalHours, int minutes, int seconds)
    {
        var ok = TimeSpanParser.TryParse(input, out var value);

        Assert.True(ok);
        Assert.Equal(new TimeSpan(totalHours, minutes, seconds), value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-timespan")]
    public void TryParse_InvalidValues_ReturnsFalse(string input)
    {
        var ok = TimeSpanParser.TryParse(input, out var value);

        Assert.False(ok);
        Assert.Equal(default, value);
    }
}
