using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Application.Interfaces.Admin;

public interface ITopicAdmin
{
    Task<IReadOnlyList<TopicSummary>> ListTopicsAsync(string connectionString, CancellationToken cancellationToken = default);
    Task CreateTopicAsync(string connectionString, string topicName, CancellationToken cancellationToken = default);
    Task DeleteTopicAsync(string connectionString, string topicName, CancellationToken cancellationToken = default);

    Task<TopicSettings> GetTopicSettingsAsync(string connectionString, string topicName, CancellationToken cancellationToken = default);
    Task UpdateTopicSettingsAsync(string connectionString, string topicName, TimeSpan defaultMessageTimeToLive, CancellationToken cancellationToken = default);
    Task UpdateTopicPropertiesAsync(string connectionString, string topicName, bool enableBatchedOperations, CancellationToken cancellationToken = default);
}
