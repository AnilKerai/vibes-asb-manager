using Microsoft.Extensions.Options;
using Vibes.ASBManager.Domain.Models;
using Vibes.ASBManager.Infrastructure.Storage.File.IO;
using Vibes.ASBManager.Infrastructure.Storage.File.Options;
using Vibes.ASBManager.Infrastructure.Storage.File.Storage;

// Avoid collision with test namespace segment 'File'
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;

namespace Vibes.ASBManager.Tests.Unit.Infrastructure.Storage.File;

public class JsonConnectionStoreTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _filePath;

    public JsonConnectionStoreTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "asbmanager-tests", Guid.NewGuid().ToString("n"));
        _filePath = Path.Combine(_rootDir, "connections.json");
    }

    private static JsonConnectionStore CreateStore(string path)
    {
        var opts = new JsonFileStorageOptions { FilePath = path };
        var monitor = new StaticOptionsMonitor<JsonFileStorageOptions>(opts);
        return new JsonConnectionStore(monitor, new FileSystemAdapter());
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private readonly T _current;
        public StaticOptionsMonitor(T current) => _current = current;
        public T CurrentValue => _current;
        public T Get(string? name) => _current;
        public IDisposable? OnChange(Action<T, string?> listener) => NullDisposable.Instance;
        private sealed class NullDisposable : IDisposable { public static readonly NullDisposable Instance = new(); public void Dispose() { } }
    }

    public void Dispose()
    {
        try { if (IODirectory.Exists(_rootDir)) IODirectory.Delete(_rootDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task Save_populates_Id_and_CreatedUtc_when_missing()
    {
        var store = CreateStore(_filePath);
        var c = new ConnectionInfo
        {
            Id = string.Empty,
            Name = "Dev",
            ConnectionString = "Endpoint=sb://foo/" // not validated by store
        };

        await store.SaveAsync(c);

        var all = await store.GetAllAsync();
        var saved = Assert.Single(all);
        Assert.False(string.IsNullOrWhiteSpace(saved.Id));
        Assert.NotEqual(default, saved.CreatedUtc);
    }

    [Fact]
    public async Task GetAll_orders_pinned_first_then_by_name_case_insensitive()
    {
        var store = CreateStore(_filePath);
        await store.SaveAsync(new ConnectionInfo { Name = "bravo", Pinned = false });
        await store.SaveAsync(new ConnectionInfo { Name = "Alpha", Pinned = false });
        await store.SaveAsync(new ConnectionInfo { Name = "Zulu", Pinned = true });
        await store.SaveAsync(new ConnectionInfo { Name = "Echo", Pinned = true });

        var all = await store.GetAllAsync();
        Assert.Equal(4, all.Count);
        
        // Pinned group alphabetical: Echo, Zulu
        Assert.True(all[0].Pinned && all[1].Pinned);
        Assert.Equal(new[] { "Echo", "Zulu" }, all.Take(2).Select(x => x.Name));
        
        // Non-pinned alphabetical: Alpha, bravo
        Assert.False(all[2].Pinned);
        Assert.False(all[3].Pinned);
        Assert.Equal(new[] { "Alpha", "bravo" }, all.Skip(2).Select(x => x.Name));
    }

    [Fact]
    public async Task Get_and_Delete_by_id_work()
    {
        var store = CreateStore(_filePath);
        var c1 = new ConnectionInfo { Name = "one" };
        var c2 = new ConnectionInfo { Name = "two" };
        await store.SaveAsync(c1);
        await store.SaveAsync(c2);

        var again1 = await store.GetAsync(c1.Id);
        Assert.NotNull(again1);
        Assert.Equal("one", again1!.Name);

        await store.DeleteAsync(c1.Id);
        var after = await store.GetAllAsync();
        Assert.Single(after);
        Assert.Equal("two", after[0].Name);
        Assert.Null(await store.GetAsync(c1.Id));
    }

    [Fact]
    public async Task Creates_directory_on_first_write()
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Assert.False(IODirectory.Exists(dir));

        var store = CreateStore(_filePath);
        await store.SaveAsync(new ConnectionInfo { Name = "X" });

        Assert.True(IODirectory.Exists(dir));
        Assert.True(IOFile.Exists(_filePath));
    }

    [Fact]
    public async Task Corrupted_json_returns_empty_list()
    {
        IODirectory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await IOFile.WriteAllTextAsync(_filePath, "{ not: valid json ]");

        var store = CreateStore(_filePath);
        var list = await store.GetAllAsync();
        Assert.Empty(list);
    }

    [Fact]
    public async Task Concurrent_saves_do_not_corrupt_file()
    {
        var store = CreateStore(_filePath);
        var tasks = Enumerable.Range(0, 50)
            .Select(i => store.SaveAsync(new ConnectionInfo { Name = $"Conn-{i}" }));

        await Task.WhenAll(tasks);

        var all = await store.GetAllAsync();
        Assert.Equal(50, all.Count);
        
        var again = await store.GetAllAsync();
        Assert.Equal(50, again.Count);
    }
}