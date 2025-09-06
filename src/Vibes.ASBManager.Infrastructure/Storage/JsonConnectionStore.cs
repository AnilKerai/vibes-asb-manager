using System.Text.Json;
using System.Text.Json.Serialization;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Domain.Models;
using System.Diagnostics;

namespace Vibes.ASBManager.Infrastructure.Storage;

public sealed class JsonConnectionStore(
    string filePath
) : IConnectionStore
{
    private readonly string _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    private Root _cache = new() { Version = 2 };
    private bool _loaded;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public async Task<IReadOnlyList<ConnectionInfo>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);
        return _cache.Connections.ToList();
    }

    public async Task<ConnectionInfo?> GetAsync(string id, CancellationToken ct = default)
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        await EnsureLoadedAsync(ct).ConfigureAwait(false);
        return _cache.Connections.FirstOrDefault(c => c.Id == id);
    }

    public async Task SaveAsync(ConnectionInfo connection, CancellationToken ct = default)
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        connection.NormalizeTags();
        await EnsureLoadedAsync(ct).ConfigureAwait(false);
        var existing = _cache.Connections.FirstOrDefault(c => c.Id == connection.Id);
        if (existing is null)
        {
            connection.CreatedUtc = DateTimeOffset.UtcNow;
            _cache.Connections.Add(connection);
        }
        else
        {
            // Preserve CreatedUtc if not set
            if (connection.CreatedUtc == default)
                connection.CreatedUtc = existing.CreatedUtc;
            _cache.Connections[_cache.Connections.FindIndex(c => c.Id == connection.Id)] = connection;
        }
        await PersistAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        await EnsureLoadedAsync(ct).ConfigureAwait(false);
        _cache.Connections.RemoveAll(c => c.Id == id);
        await PersistAsync(ct).ConfigureAwait(false);
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loaded) return;
        if (!File.Exists(_filePath))
        {
            var appDataDir = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(appDataDir);
            // Legacy locations migration: try parent and grandparent of AppDataDirectory
            var parentDir = Directory.GetParent(appDataDir);
            var fileName = Path.GetFileName(_filePath);
            var candidates = new List<string>();
            if (parentDir is not null)
            {
                candidates.Add(Path.Combine(parentDir.FullName, fileName));
                var grandParent = Directory.GetParent(parentDir.FullName);
                if (grandParent is not null)
                    candidates.Add(Path.Combine(grandParent.FullName, fileName));
            }
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    try { Debug.WriteLine($"[JsonConnectionStore] Migrating connections.json from '{candidate}' to '{_filePath}'"); File.Copy(candidate, _filePath, overwrite: true); } catch (Exception ex) { Debug.WriteLine($"[JsonConnectionStore] Migration copy failed: {ex.Message}"); }
                    break;
                }
            }
            if (!File.Exists(_filePath))
            {
                _cache = new Root { Version = 2 };
                _loaded = true;
                Debug.WriteLine($"[JsonConnectionStore] No connections.json found at '{_filePath}'. Initialized empty store.");
                return;
            }
        }
        // Read all text to support flexible deserialization/migration
        var json = await File.ReadAllTextAsync(_filePath, ct).ConfigureAwait(false);
        Root? root = null;
        try
        {
            root = JsonSerializer.Deserialize<Root>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[JsonConnectionStore] Failed to deserialize Root format: {ex.Message}. Will try fallback.");
        }

        if (root is null || (root.Version == 0 && (root.Connections is null || root.Connections.Count == 0)))
        {
            // Fallback: older files may be a top-level array of ConnectionInfo
            try
            {
                var list = JsonSerializer.Deserialize<List<ConnectionInfo>>(json, JsonOpts);
                if (list is not null)
                {
                    foreach (var c in list)
                        c.Tags ??= new List<string>();
                    _cache = new Root { Version = 2, Connections = list };
                    await PersistAsync(ct).ConfigureAwait(false); // migrate to current format
                    _loaded = true;
                    Debug.WriteLine($"[JsonConnectionStore] Migrated legacy list format. Loaded {list.Count} connections.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JsonConnectionStore] Fallback list deserialization failed: {ex.Message}");
            }
        }

        _cache = root ?? new Root { Version = 2 };
        if (_cache.Version < 2)
        {
            // migrate to v2 (add tags if missing)
            foreach (var c in _cache.Connections)
                c.Tags ??= new List<string>();
            _cache.Version = 2;
            await PersistAsync(ct).ConfigureAwait(false);
        }
        _loaded = true;
        Debug.WriteLine($"[JsonConnectionStore] Loaded {_cache.Connections.Count} connections from '{_filePath}'.");
    }

    private async Task PersistAsync(CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);
        var tmp = _filePath + ".tmp";
        await using (var fs = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(fs, _cache, JsonOpts, ct).ConfigureAwait(false);
        }
        // Replace destination
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
        File.Move(tmp, _filePath);
    }

    private sealed class Root
    {
        public int Version { get; set; }
        public List<ConnectionInfo> Connections { get; set; } = new();
    }
}
