using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Domain.Models;

namespace Vibes.ASBManager.Infrastructure.Storage;

public sealed class SqlConnectionStore(string connectionString) : IConnectionStore
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public async Task<IReadOnlyList<ConnectionInfo>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);
        const string sql = @"select 
                id as Id,
                name as Name,
                connection_string as ConnectionString,
                tags_json as TagsJson,
                created_utc as CreatedUtc,
                last_used_utc as LastUsedUtc
            from connections
            order by name";
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<DbRow>(new CommandDefinition(sql, cancellationToken: ct));
        var list = new List<ConnectionInfo>();
        foreach (var r in rows)
        {
            list.Add(new ConnectionInfo
            {
                Id = r.Id,
                Name = r.Name,
                ConnectionString = r.ConnectionString,
                Tags = ParseTags(r.TagsJson),
                CreatedUtc = r.CreatedUtc,
                LastUsedUtc = r.LastUsedUtc
            });
        }
        return list;
    }

    public async Task<ConnectionInfo?> GetAsync(string id, CancellationToken ct = default)
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        await EnsureSchemaAsync(ct).ConfigureAwait(false);
        const string sql = @"select 
                id as Id,
                name as Name,
                connection_string as ConnectionString,
                tags_json as TagsJson,
                created_utc as CreatedUtc,
                last_used_utc as LastUsedUtc
            from connections
            where id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        var row = await conn.QuerySingleOrDefaultAsync<DbRow>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
        if (row is null) return null;
        return new ConnectionInfo
        {
            Id = row.Id,
            Name = row.Name,
            ConnectionString = row.ConnectionString,
            Tags = ParseTags(row.TagsJson),
            CreatedUtc = row.CreatedUtc,
            LastUsedUtc = row.LastUsedUtc
        };
    }

    public async Task SaveAsync(ConnectionInfo connection, CancellationToken ct = default)
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        connection.NormalizeTags();
        await EnsureSchemaAsync(ct).ConfigureAwait(false);
        var tagsJson = SerializeTags(connection.Tags);
        var now = DateTimeOffset.UtcNow;
        // Preserve CreatedUtc if caller didn't set; fall back to now
        var created = connection.CreatedUtc == default ? now : connection.CreatedUtc;

        const string upsert = @"insert into connections (id, name, connection_string, tags_json, created_utc, last_used_utc)
values (@Id, @Name, @ConnectionString, @TagsJson, @CreatedUtc, @LastUsedUtc)
on conflict (id) do update set
    name = excluded.name,
    connection_string = excluded.connection_string,
    tags_json = excluded.tags_json,
    -- preserve original created_utc when updating
    created_utc = connections.created_utc,
    last_used_utc = excluded.last_used_utc";

        var args = new
        {
            Id = connection.Id,
            Name = connection.Name,
            ConnectionString = connection.ConnectionString,
            TagsJson = tagsJson,
            CreatedUtc = created,
            LastUsedUtc = connection.LastUsedUtc
        };

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition(upsert, args, cancellationToken: ct));
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        await EnsureSchemaAsync(ct).ConfigureAwait(false);
        const string sql = "delete from connections where id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    private static List<string> ParseTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? new List<string>();
            return list.Where(s => !string.IsNullOrWhiteSpace(s))
                       .Select(s => s.Trim().ToLowerInvariant())
                       .Distinct()
                       .OrderBy(s => s)
                       .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string SerializeTags(List<string>? tags)
    {
        tags ??= new List<string>();
        return JsonSerializer.Serialize(tags, JsonOpts);
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        const string createTable = @"create table if not exists connections (
            id text primary key,
            name text not null,
            connection_string text null,
            tags_json text null,
            created_utc timestamptz not null,
            last_used_utc timestamptz null
        )";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition(createTable, cancellationToken: ct));
    }

    private sealed class DbRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? ConnectionString { get; init; }
        public string? TagsJson { get; init; }
        public DateTimeOffset CreatedUtc { get; init; }
        public DateTimeOffset? LastUsedUtc { get; init; }
    }
}
