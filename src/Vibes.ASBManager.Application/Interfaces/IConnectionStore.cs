using Vibes.ASBManager.Domain.Models;

namespace Vibes.ASBManager.Application.Interfaces;

public interface IConnectionStore
{
    Task<IReadOnlyList<ConnectionInfo>> GetAllAsync(CancellationToken ct = default);
    Task<ConnectionInfo?> GetAsync(string id, CancellationToken ct = default);
    Task SaveAsync(ConnectionInfo connection, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
