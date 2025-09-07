using Vibes.ASBManager.Infrastructure.Storage.File.IO;

namespace Vibes.ASBManager.Tests.Unit.TestDoubles;

public sealed class FakeFileSystem : IFileSystem
{
    private readonly object _sync = new();
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public bool Exists(string path)
    {
        lock (_sync)
        {
            return _files.ContainsKey(path);
        }
    }

    public Stream OpenRead(string path)
    {
        lock (_sync)
        {
            if (!_files.TryGetValue(path, out var bytes))
                throw new FileNotFoundException(path);
            // Return a new stream so callers can dispose independently
            return new MemoryStream(bytes, writable: false);
        }
    }

    public Stream CreateWrite(string path)
    {
        return new CommitOnDisposeStream(this, path);
    }

    public void Replace(string sourceFileName, string destinationFileName)
    {
        lock (_sync)
        {
            if (!_files.TryGetValue(sourceFileName, out var src))
                throw new FileNotFoundException(sourceFileName);
            _files[destinationFileName] = src;
            _files.Remove(sourceFileName);
        }
    }

    public void Move(string sourceFileName, string destinationFileName)
    {
        lock (_sync)
        {
            if (!_files.TryGetValue(sourceFileName, out var src))
                throw new FileNotFoundException(sourceFileName);
            if (_files.ContainsKey(destinationFileName))
                throw new IOException($"Destination exists: {destinationFileName}");
            _files[destinationFileName] = src;
            _files.Remove(sourceFileName);
        }
    }

    // Test helpers
    public void SetFileBytes(string path, byte[] data)
    {
        lock (_sync)
        {
            _files[path] = data;
        }
    }

    public byte[]? GetFileBytes(string path)
    {
        lock (_sync)
        {
            return _files.TryGetValue(path, out var data) ? data : null;
        }
    }

    private sealed class CommitOnDisposeStream : MemoryStream
    {
        private readonly FakeFileSystem _fs;
        private readonly string _path;
        private bool _committed;

        public CommitOnDisposeStream(FakeFileSystem fs, string path)
        {
            _fs = fs;
            _path = path;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_committed)
            {
                var data = ToArray();
                lock (_fs._sync)
                {
                    _fs._files[_path] = data;
                }
                _committed = true;
            }
            base.Dispose(disposing);
        }
    }
}
