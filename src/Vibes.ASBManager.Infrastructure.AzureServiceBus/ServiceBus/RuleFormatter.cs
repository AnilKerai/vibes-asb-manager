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
            case SqlRuleFilter sqlFilter:
            {
                var sqlExpression = sqlFilter.SqlExpression?.Trim();
                if (string.Equals(sqlExpression, "1=1", StringComparison.OrdinalIgnoreCase)) return "True";
                if (string.Equals(sqlExpression, "1=0", StringComparison.OrdinalIgnoreCase)) return "False";
                return $"SQL: {sqlFilter.SqlExpression}";
            }
            case CorrelationRuleFilter correlationFilter:
            {
                var filterParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(correlationFilter.CorrelationId)) filterParts.Add($"CorrelationId='{correlationFilter.CorrelationId}'");
                if (!string.IsNullOrWhiteSpace(correlationFilter.Subject)) filterParts.Add($"Subject='{correlationFilter.Subject}'");
                if (!string.IsNullOrWhiteSpace(correlationFilter.To)) filterParts.Add($"To='{correlationFilter.To}'");
                if (!string.IsNullOrWhiteSpace(correlationFilter.ReplyTo)) filterParts.Add($"ReplyTo='{correlationFilter.ReplyTo}'");
                if (!string.IsNullOrWhiteSpace(correlationFilter.ReplyToSessionId)) filterParts.Add($"ReplyToSessionId='{correlationFilter.ReplyToSessionId}'");
                if (!string.IsNullOrWhiteSpace(correlationFilter.SessionId)) filterParts.Add($"SessionId='{correlationFilter.SessionId}'");
                if (!string.IsNullOrWhiteSpace(correlationFilter.ContentType)) filterParts.Add($"ContentType='{correlationFilter.ContentType}'");
                if (correlationFilter.ApplicationProperties is { Count: > 0 })
                {
                    var props = string.Join(", ", correlationFilter.ApplicationProperties.Select(property => $"{property.Key}={property.Value}"));
                    filterParts.Add($"AppProps: {props}");
                }
                return filterParts.Count == 0 ? "Correlation (no fields)" : $"Correlation: {string.Join(", ", filterParts)}";
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
            SqlRuleAction sqlAction => $"SQL: {sqlAction.SqlExpression}",
            _ => action.GetType().Name
        };
    }
}
