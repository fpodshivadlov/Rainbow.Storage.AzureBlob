using System.Collections.Generic;
using System.IO;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Rainbow.Storage.AzureBlob.Provider
{
    public class AzureProvider : IAzureProvider
    {
        private readonly CloudBlobContainer cloudBlobContainer;

        public AzureProvider(CloudBlobContainer cloudBlobContainer)
        {
            this.cloudBlobContainer = cloudBlobContainer;
        }

        public IEnumerable<IListBlobItem> EnumerateBlobs(string prefix, SearchOption searchOption, int? maxResultsPerTime = null)
        {
            BlobContinuationToken blobContinuationToken = null;
            do
            {
                BlobResultSegment results = cloudBlobContainer.ListBlobsSegmented(
                    prefix,
                    searchOption == SearchOption.AllDirectories,
                    BlobListingDetails.None,
                    maxResultsPerTime,
                    blobContinuationToken,
                    new BlobRequestOptions(),
                    null
                );
                
                blobContinuationToken = results.ContinuationToken;
                foreach (IListBlobItem item in results.Results)
                {
                    yield return item;
                }
            }
            while (blobContinuationToken != null);
        }

        public bool BlobExists(string blobName)
        {
            CloudBlob blobReference = this.cloudBlobContainer.GetBlobReference(blobName);
            
            return blobReference.Exists();
        }
    }
}