namespace Vibes.ASBManager.Infrastructure.Storage.File.IO;

public sealed class FileSystemAdapter : IFileSystem
{
    public bool Exists(string path) => System.IO.File.Exists(path);

    public Stream OpenRead(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

    public Stream CreateWrite(string path) => new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

    public void Replace(string sourceFileName, string destinationFileName) => System.IO.File.Replace(sourceFileName, destinationFileName, destinationBackupFileName: null);

    public void Move(string sourceFileName, string destinationFileName) => System.IO.File.Move(sourceFileName, destinationFileName);
}
