using Vibes.ASBManager.Web.Models;

namespace Vibes.ASBManager.Tests.Unit.Web.Models;

public class SettingsSanitizerTests
{
    [Theory]
    [InlineData(0, 1)]    // floored: Service Bus requires >= 1
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    public void MaxDeliveryCount_FloorsAtOne(int input, int expected)
        => Assert.Equal(expected, SettingsSanitizer.MaxDeliveryCount(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForwardAddress_BlankBecomesNull(string? input)
        => Assert.Null(SettingsSanitizer.ForwardAddress(input));

    [Theory]
    [InlineData("target-queue", "target-queue")]
    [InlineData("  target-queue  ", "target-queue")]
    public void ForwardAddress_TrimsWhitespace(string input, string expected)
        => Assert.Equal(expected, SettingsSanitizer.ForwardAddress(input));
}
