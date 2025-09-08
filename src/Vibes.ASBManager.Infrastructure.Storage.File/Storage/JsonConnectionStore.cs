using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Application.Interfaces.Connection;
using Vibes.ASBManager.Domain.Models;
using Vibes.ASBManager.Infrastructure.Storage.File.IO;
using Vibes.ASBManager.Infrastructure.Storage.File.Options;

namespace Vibes.ASBManager.Infrastructure.Storage.File.Storage;

public sealed class JsonConnectionStore(
    IOptionsMonitor<JsonFileStorageOptions> storeOptions, 
    IFileSystem fileSystem
) : IConnectionStore
{
    private readonly string _filePath = string.IsNullOrWhiteSpace(storeOptions.CurrentValue.FilePath)
        ? throw new ArgumentException("JsonFileStorageOptions.FilePath must be provided", nameof(storeOptions))
        : storeOptions.CurrentValue.FilePath;
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<IReadOnlyList<ConnectionInfo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connections = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return connections
                .OrderByDescending(c => c.Pinned)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<ConnectionInfo?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connections = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return connections.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAsync(ConnectionInfo connection, CancellationToken cancellationToken = default)
    {
        if (connection is null) return;
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connections = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var existingIndex = connections.FindIndex(c => string.Equals(c.Id, connection.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                connections[existingIndex] = connection;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(connection.Id))
                    connection.Id = Guid.NewGuid().ToString("n");
                if (connection.CreatedUtc == default)
                    connection.CreatedUtc = DateTimeOffset.UtcNow;
                connections.Add(connection);
            }
            await WriteAsync(connections, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connections = await ReadAsync(cancellationToken).ConfigureAwait(false);
            connections.RemoveAll(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            await WriteAsync(connections, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<ConnectionInfo>> ReadAsync(CancellationToken ct)
    {
        EnsureDirectoryExists();
        if (!_fileSystem.Exists(_filePath))
        {
            return new List<ConnectionInfo>();
        }
        await using var readStream = _fileSystem.OpenRead(_filePath);
        try
        {
            var data = await JsonSerializer.DeserializeAsync<List<ConnectionInfo>>(readStream, SerializerOptions, ct).ConfigureAwait(false);
            return data ?? new List<ConnectionInfo>();
        }
        catch
        {
            return new List<ConnectionInfo>();
        }
    }

    private async Task WriteAsync(List<ConnectionInfo> connections, CancellationToken ct)
    {
        EnsureDirectoryExists();
        // Write to temp then move to avoid partial writes
        var tempFilePath = _filePath + ".tmp";
        await using (var writeStream = _fileSystem.CreateWrite(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(writeStream, connections, SerializerOptions, ct).ConfigureAwait(false);
        }
        if (_fileSystem.Exists(_filePath))
        {
            _fileSystem.Replace(tempFilePath, _filePath);
        }
        else
        {
            _fileSystem.Move(tempFilePath, _filePath);
        }
    }

    private void EnsureDirectoryExists()
    {
        var directoryPath = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }
}
