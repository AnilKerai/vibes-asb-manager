using Azure.Messaging.ServiceBus.Administration;
using Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

namespace Vibes.ASBManager.Tests.Unit.Infrastructure.AzureServiceBus;

public class RuleFormatterTests
{
    private readonly RuleFormatter _sut = new();

    [Fact]
    public void FormatFilter_Sql_TrueFalse_Shortcuts()
    {
        Assert.Equal("True", _sut.FormatFilter(new SqlRuleFilter("1=1")));
        Assert.Equal("False", _sut.FormatFilter(new SqlRuleFilter("1=0")));
    }

    [Fact]
    public void FormatFilter_Sql_General()
    {
        var f = new SqlRuleFilter("sys.Label = 'X'");
        Assert.Equal("SQL: sys.Label = 'X'", _sut.FormatFilter(f));
    }

    [Fact]
    public void FormatFilter_Correlation_Empty()
    {
        var f = new CorrelationRuleFilter();
        Assert.Equal("Correlation (no fields)", _sut.FormatFilter(f));
    }

    [Fact]
    public void FormatFilter_Correlation_WithFieldsAndAppProps()
    {
        var f = new CorrelationRuleFilter
        {
            CorrelationId = "abc",
            Subject = "foo",
            To = "bar",
            ReplyTo = "baz",
            ReplyToSessionId = "rts",
            SessionId = "sess",
            ContentType = "ct"
        };
        f.ApplicationProperties["k1"] = "v1";
        f.ApplicationProperties["k2"] = "v2";

        var s = _sut.FormatFilter(f);
        Assert.Contains("CorrelationId='abc'", s);
        Assert.Contains("Subject='foo'", s);
        Assert.Contains("To='bar'", s);
        Assert.Contains("ReplyTo='baz'", s);
        Assert.Contains("ReplyToSessionId='rts'", s);
        Assert.Contains("SessionId='sess'", s);
        Assert.Contains("ContentType='ct'", s);
        Assert.Contains("AppProps: k1=v1, k2=v2", s);
    }

    [Fact]
    public void FormatAction_Null_And_Sql()
    {
        Assert.Null(_sut.FormatAction(null));
        var a = new SqlRuleAction("set sys.To = 'x'");
        Assert.Equal("SQL: set sys.To = 'x'", _sut.FormatAction(a));
    }
}
