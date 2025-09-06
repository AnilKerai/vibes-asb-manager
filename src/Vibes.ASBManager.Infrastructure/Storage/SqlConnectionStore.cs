using Dapper;
using Npgsql;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Domain.Models;

namespace Vibes.ASBManager.Infrastructure.Storage;

public sealed class SqlConnectionStore(string connectionString) : IConnectionStore
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<IReadOnlyList<ConnectionInfo>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);
        const string sql = @"select 
                id as Id,
                name as Name,
                connection_string as ConnectionString,
                created_utc as CreatedUtc,
                last_used_utc as LastUsedUtc,
                pinned as Pinned
            from connections
            order by pinned desc, name";
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
                CreatedUtc = r.CreatedUtc,
                LastUsedUtc = r.LastUsedUtc,
                Pinned = r.Pinned
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
                created_utc as CreatedUtc,
                last_used_utc as LastUsedUtc,
                pinned as Pinned
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
            CreatedUtc = row.CreatedUtc,
            LastUsedUtc = row.LastUsedUtc,
            Pinned = row.Pinned
        };
    }

    public async Task SaveAsync(ConnectionInfo connection, CancellationToken ct = default)
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        await EnsureSchemaAsync(ct).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        // Preserve CreatedUtc if caller didn't set; fall back to now
        var created = connection.CreatedUtc == default ? now : connection.CreatedUtc;

        const string upsert = @"insert into connections (id, name, connection_string, created_utc, last_used_utc, pinned)
values (@Id, @Name, @ConnectionString, @CreatedUtc, @LastUsedUtc, @Pinned)
on conflict (id) do update set
    name = excluded.name,
    connection_string = excluded.connection_string,
    -- preserve original created_utc when updating
    created_utc = connections.created_utc,
    last_used_utc = excluded.last_used_utc,
    pinned = excluded.pinned";

        var args = new
        {
            Id = connection.Id,
            Name = connection.Name,
            ConnectionString = connection.ConnectionString,
            CreatedUtc = created,
            LastUsedUtc = connection.LastUsedUtc,
            Pinned = connection.Pinned
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

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        const string createTable = @"create table if not exists connections (
            id text primary key,
            name text not null,
            connection_string text null,
            created_utc timestamptz not null,
            last_used_utc timestamptz null,
            pinned boolean not null default false
        )";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition(createTable, cancellationToken: ct));
        // Migrate existing tables to include new columns
        const string addPinned = "alter table if exists connections add column if not exists pinned boolean not null default false";
        await conn.ExecuteAsync(new CommandDefinition(addPinned, cancellationToken: ct));
    }

    private sealed class DbRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? ConnectionString { get; init; }
        public DateTimeOffset CreatedUtc { get; init; }
        public DateTimeOffset? LastUsedUtc { get; init; }
        public bool Pinned { get; init; }
    }
}
