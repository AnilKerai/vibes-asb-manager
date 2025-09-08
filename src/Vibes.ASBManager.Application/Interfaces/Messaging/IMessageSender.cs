namespace Vibes.ASBManager.Application.Interfaces.Messaging;

public interface IMessageSender
{
    Task SendToQueueAsync(string connectionString, string queueName, string body, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, DateTimeOffset? scheduledEnqueueTime = null, CancellationToken cancellationToken = default);
    Task SendToTopicAsync(string connectionString, string topicName, string body, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, DateTimeOffset? scheduledEnqueueTime = null, CancellationToken cancellationToken = default);
}
