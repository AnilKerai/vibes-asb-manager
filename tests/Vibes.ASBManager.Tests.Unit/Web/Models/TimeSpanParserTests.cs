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

    [Theory]
    [InlineData(0, 1, 0, 0, "01:00:00")]
    [InlineData(1, 0, 0, 0, "1.00:00:00")]
    [InlineData(0, 0, 1, 30, "00:01:30")]
    public void Format_RendersInvariantConstantForm(int days, int hours, int minutes, int seconds, string expected)
        => Assert.Equal(expected, TimeSpanParser.Format(new TimeSpan(days, hours, minutes, seconds)));

    [Theory]
    [InlineData("1.02:03:04")]
    [InlineData("00:01:00")]
    [InlineData("10675199.02:48:05")]
    public void Format_RoundTripsThroughTryParse(string input)
    {
        Assert.True(TimeSpanParser.TryParse(input, out var parsed));

        var ok = TimeSpanParser.TryParse(TimeSpanParser.Format(parsed), out var reparsed);

        Assert.True(ok);
        Assert.Equal(parsed, reparsed);
    }
}
