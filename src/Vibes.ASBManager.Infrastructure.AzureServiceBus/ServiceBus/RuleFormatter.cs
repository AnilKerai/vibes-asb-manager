using Azure.Messaging.ServiceBus.Administration;

namespace Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

public interface IRuleFormatter
{
    string FormatFilter(RuleFilter filter);
    string? FormatAction(RuleAction? action);
}

public sealed class RuleFormatter : IRuleFormatter
{
    public string FormatFilter(RuleFilter filter)
    {
        switch (filter)
        {
            case SqlRuleFilter sql:
            {
                var expr = sql.SqlExpression?.Trim();
                if (string.Equals(expr, "1=1", StringComparison.OrdinalIgnoreCase)) return "True";
                if (string.Equals(expr, "1=0", StringComparison.OrdinalIgnoreCase)) return "False";
                return $"SQL: {sql.SqlExpression}";
            }
            case CorrelationRuleFilter corr:
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(corr.CorrelationId)) parts.Add($"CorrelationId='{corr.CorrelationId}'");
                if (!string.IsNullOrWhiteSpace(corr.Subject)) parts.Add($"Subject='{corr.Subject}'");
                if (!string.IsNullOrWhiteSpace(corr.To)) parts.Add($"To='{corr.To}'");
                if (!string.IsNullOrWhiteSpace(corr.ReplyTo)) parts.Add($"ReplyTo='{corr.ReplyTo}'");
                if (!string.IsNullOrWhiteSpace(corr.ReplyToSessionId)) parts.Add($"ReplyToSessionId='{corr.ReplyToSessionId}'");
                if (!string.IsNullOrWhiteSpace(corr.SessionId)) parts.Add($"SessionId='{corr.SessionId}'");
                if (!string.IsNullOrWhiteSpace(corr.ContentType)) parts.Add($"ContentType='{corr.ContentType}'");
                if (corr.ApplicationProperties is { Count: > 0 })
                {
                    var props = string.Join(", ", corr.ApplicationProperties.Select(kv => $"{kv.Key}={kv.Value}"));
                    parts.Add($"AppProps: {props}");
                }
                return parts.Count == 0 ? "Correlation (no fields)" : $"Correlation: {string.Join(", ", parts)}";
            }
            default:
                return filter.GetType().Name;
        }
    }

    public string? FormatAction(RuleAction? action)
    {
        if (action is null) return null;
        return action switch
        {
            SqlRuleAction sql => $"SQL: {sql.SqlExpression}",
            _ => action.GetType().Name
        };
    }
}
