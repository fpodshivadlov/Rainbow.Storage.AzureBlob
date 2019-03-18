using System.Collections.Generic;
using System.IO;

namespace Rainbow.Storage.AzureBlob.Manager
{
    public interface IAzureManager
    {
        IEnumerable<string> EnumerateFiles(string path, string extension, SearchOption searchOption);
        bool FileExists(string filePath);
        bool DirectoryExists(string directoryPath);
        Stream GetFileStream(string filePath, bool openRead = true);
        void EnsureDirectory(string physicalRootPath);
    }
}