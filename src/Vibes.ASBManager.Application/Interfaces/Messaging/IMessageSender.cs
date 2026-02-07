namespace Vibes.ASBManager.Application.Interfaces.Messaging;

public interface IMessageSender
{
    Task SendToQueueAsync(string connectionString, string queueName, string body, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, DateTimeOffset? scheduledEnqueueTime = null, CancellationToken cancellationToken = default);
    Task SendToTopicAsync(string connectionString, string topicName, string body, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, DateTimeOffset? scheduledEnqueueTime = null, CancellationToken cancellationToken = default);
    Task SendBatchToQueueAsync(string connectionString, string queueName, string body, int count, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, CancellationToken cancellationToken = default);
    Task SendBatchToTopicAsync(string connectionString, string topicName, string body, int count, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, CancellationToken cancellationToken = default);
}
