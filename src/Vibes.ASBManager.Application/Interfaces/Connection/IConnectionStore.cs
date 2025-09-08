using Vibes.ASBManager.Domain.Models;

namespace Vibes.ASBManager.Application.Interfaces.Connection;

public interface IConnectionStore
{
    Task<IReadOnlyList<ConnectionInfo>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ConnectionInfo?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task SaveAsync(ConnectionInfo connection, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
