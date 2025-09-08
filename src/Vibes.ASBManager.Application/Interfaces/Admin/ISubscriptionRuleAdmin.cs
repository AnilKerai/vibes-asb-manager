using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Application.Interfaces.Admin;

public interface ISubscriptionRuleAdmin
{
    Task<IReadOnlyList<SubscriptionRuleInfo>> ListSubscriptionRulesAsync(string connectionString, string topicName, string subscriptionName, CancellationToken cancellationToken = default);
    Task CreateSubscriptionSqlRuleAsync(string connectionString, string topicName, string subscriptionName, string ruleName, string sqlExpression, string? sqlAction = null, CancellationToken cancellationToken = default);
    Task CreateSubscriptionCorrelationRuleAsync(
        string connectionString,
        string topicName,
        string subscriptionName,
        string ruleName,
        string? correlationId,
        string? subject,
        string? to,
        string? replyTo,
        string? replyToSessionId,
        string? sessionId,
        string? contentType,
        Dictionary<string, string>? applicationProperties = null,
        CancellationToken cancellationToken = default);
    Task DeleteSubscriptionRuleAsync(string connectionString, string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default);
}
