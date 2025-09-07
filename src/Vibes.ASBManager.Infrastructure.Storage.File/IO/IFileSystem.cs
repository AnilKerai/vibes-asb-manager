namespace Vibes.ASBManager.Infrastructure.Storage.File.IO;

public interface IFileSystem
{
    bool Exists(string path);
    Stream OpenRead(string path);
    Stream CreateWrite(string path);
    void Replace(string sourceFileName, string destinationFileName);
    void Move(string sourceFileName, string destinationFileName);
}
