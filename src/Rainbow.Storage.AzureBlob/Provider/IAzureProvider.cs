using System.Collections.Generic;
using System.IO;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Rainbow.Storage.AzureBlob.Provider
{
    public interface IAzureProvider
    {
        IEnumerable<IListBlobItem> EnumerateBlobs(string prefix, SearchOption searchOption, int? maxResultsPerTime = null);
        
        bool BlobExists(string blobName);
    }
}