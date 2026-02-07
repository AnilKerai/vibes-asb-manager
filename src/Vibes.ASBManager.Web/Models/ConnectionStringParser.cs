namespace Vibes.ASBManager.Web.Models;

public static class ConnectionStringParser
{
    public static string? GetEndpoint(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        try
        {
            var part = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(p => p.TrimStart().StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase));
            if (part is null) return null;
            var idx = part.IndexOf('=');
            if (idx < 0 || idx + 1 >= part.Length) return null;
            var value = part[(idx + 1)..].Trim();
            return value;
        }
        catch
        {
            return null;
        }
    }

    public static string? GetName(string connectionString)
    {
        var endpoint = GetEndpoint(connectionString);
        if (string.IsNullOrWhiteSpace(endpoint)) return null;
        try
        {
            var uri = new Uri(endpoint);
            var host = uri.Host;
            if (string.IsNullOrWhiteSpace(host)) return null;
            var first = host.Split('.').FirstOrDefault();
            return string.IsNullOrWhiteSpace(first) ? host : first;
        }
        catch
        {
            var trimmed = endpoint.Replace("sb://", string.Empty, StringComparison.OrdinalIgnoreCase);
            var hostPart = trimmed.Split('/')[0];
            var first = hostPart.Split('.').FirstOrDefault();
            return string.IsNullOrWhiteSpace(first) ? hostPart : first;
        }
    }
}
