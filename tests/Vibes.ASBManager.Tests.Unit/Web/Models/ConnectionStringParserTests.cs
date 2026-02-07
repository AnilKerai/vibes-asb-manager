using Vibes.ASBManager.Web.Models;

namespace Vibes.ASBManager.Tests.Unit.Web.Models;

public class ConnectionStringParserTests
{
    [Fact]
    public void GetEndpoint_ReturnsEndpointValue()
    {
        var value = ConnectionStringParser.GetEndpoint("Endpoint=sb://demo.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=abc");

        Assert.Equal("sb://demo.servicebus.windows.net/", value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SharedAccessKeyName=Root;SharedAccessKey=abc")]
    public void GetEndpoint_MissingEndpoint_ReturnsNull(string connectionString)
    {
        var value = ConnectionStringParser.GetEndpoint(connectionString);

        Assert.Null(value);
    }

    [Fact]
    public void GetName_ReturnsNamespaceFromEndpoint()
    {
        var value = ConnectionStringParser.GetName("Endpoint=sb://sample.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=abc");

        Assert.Equal("sample", value);
    }

    [Fact]
    public void GetName_FallsBack_WhenEndpointNotUri()
    {
        var value = ConnectionStringParser.GetName("Endpoint=sb://local space/;SharedAccessKeyName=Root;SharedAccessKey=abc");

        Assert.Equal("local space", value);
    }

    [Fact]
    public void GetName_MissingEndpoint_ReturnsNull()
    {
        var value = ConnectionStringParser.GetName("SharedAccessKeyName=Root;SharedAccessKey=abc");

        Assert.Null(value);
    }
}
