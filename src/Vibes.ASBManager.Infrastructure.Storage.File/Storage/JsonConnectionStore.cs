using System.Text.Json;
using System.Text.Json.Serialization;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Domain.Models;

namespace Vibes.ASBManager.Infrastructure.Storage.File.Storage;

public sealed class JsonConnectionStore : IConnectionStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public JsonConnectionStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path must be provided", nameof(filePath));
        _filePath = filePath;
    }

    public async Task<IReadOnlyList<ConnectionInfo>> GetAllAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var list = await ReadAsync(ct).ConfigureAwait(false);
            return list
                .OrderByDescending(c => c.Pinned)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ConnectionInfo?> GetAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var list = await ReadAsync(ct).ConfigureAwait(false);
            return list.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(ConnectionInfo connection, CancellationToken ct = default)
    {
        if (connection is null) return;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var list = await ReadAsync(ct).ConfigureAwait(false);
            var existing = list.FindIndex(c => string.Equals(c.Id, connection.Id, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                list[existing] = connection;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(connection.Id))
                    connection.Id = Guid.NewGuid().ToString("n");
                if (connection.CreatedUtc == default)
                    connection.CreatedUtc = DateTimeOffset.UtcNow;
                list.Add(connection);
            }
            await WriteAsync(list, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var list = await ReadAsync(ct).ConfigureAwait(false);
            list.RemoveAll(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            await WriteAsync(list, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<ConnectionInfo>> ReadAsync(CancellationToken ct)
    {
        EnsureDirectory();
        if (!System.IO.File.Exists(_filePath))
        {
            return new List<ConnectionInfo>();
        }
        await using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var data = await JsonSerializer.DeserializeAsync<List<ConnectionInfo>>(fs, Options, ct).ConfigureAwait(false);
            return data ?? new List<ConnectionInfo>();
        }
        catch
        {
            return new List<ConnectionInfo>();
        }
    }

    private async Task WriteAsync(List<ConnectionInfo> list, CancellationToken ct)
    {
        EnsureDirectory();
        // Write to temp then move to avoid partial writes
        var tmp = _filePath + ".tmp";
        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(fs, list, Options, ct).ConfigureAwait(false);
        }
        if (System.IO.File.Exists(_filePath))
        {
            System.IO.File.Replace(tmp, _filePath, destinationBackupFileName: null);
        }
        else
        {
            System.IO.File.Move(tmp, _filePath);
        }
    }

    private void EnsureDirectory()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }
}
