using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Application.Interfaces;

public interface ITopicAdmin
{
    Task<IReadOnlyList<TopicSummary>> ListTopicsAsync(string connectionString, CancellationToken ct = default);
    Task CreateTopicAsync(string connectionString, string topicName, CancellationToken ct = default);
    Task DeleteTopicAsync(string connectionString, string topicName, CancellationToken ct = default);

    Task<TopicSettings> GetTopicSettingsAsync(string connectionString, string topicName, CancellationToken ct = default);
    Task UpdateTopicSettingsAsync(string connectionString, string topicName, TimeSpan defaultMessageTimeToLive, CancellationToken ct = default);
    Task UpdateTopicPropertiesAsync(string connectionString, string topicName, bool enableBatchedOperations, CancellationToken ct = default);
}
