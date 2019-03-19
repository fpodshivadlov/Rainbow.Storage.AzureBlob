using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Blob;
using Sitecore.Diagnostics;

namespace Rainbow.Storage.AzureBlob.Provider
{   
    public class CachedAzureProvider : IAzureProvider
    {
        private readonly IDictionary<string, IListBlobItem> blobsItemsCache = new ConcurrentDictionary<string, IListBlobItem>();
        private readonly CloudBlobContainer cloudBlobContainer;

        // ToDo: pass root prefix to reduce number of items
        public CachedAzureProvider(CloudBlobContainer cloudBlobContainer)
        {
            this.cloudBlobContainer = cloudBlobContainer;
        }
        
        public IEnumerable<IListBlobItem> EnumerateBlobs(string prefix, SearchOption searchOption, int? maxResultsPerTime = null)
        {
            this.EnsureCache();

            foreach (KeyValuePair<string, IListBlobItem> itemKeyValue in this.blobsItemsCache)
            {
                string name = itemKeyValue.Key;
                
                if (!string.IsNullOrEmpty(prefix) && !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string relativePath = string.IsNullOrEmpty(prefix) ? name : name.Substring(prefix.Length);
                if (searchOption == SearchOption.TopDirectoryOnly && relativePath.Trim('/').Contains('/'))
                    continue;

                yield return itemKeyValue.Value;
            }
        }

        public bool BlobExists(string blobName)
        {
            this.EnsureCache();

            return this.blobsItemsCache
                .Any(item => string.Equals(item.Key, blobName, StringComparison.OrdinalIgnoreCase) 
                             && item.Value is CloudBlockBlob);
        }

        private void EnsureCache()
        {
            if (!this.blobsItemsCache.Any())
            {
                lock (this.blobsItemsCache)
                {
                    if (!this.blobsItemsCache.Any())
                    {
                        Log.Info($"AZURE BLOB STORAGE. Creating cache (container={this.cloudBlobContainer.Name}).", this);
                        foreach (CloudBlockBlob blob in this.GetAllBlobs())
                        {
                            this.blobsItemsCache[blob.Name] = blob;
                        }
                        
                        Log.Info($"AZURE BLOB STORAGE. Cached created. Total nodes: {this.blobsItemsCache.Keys.Count} (container={this.cloudBlobContainer.Name}).", this);
                    }
                }
            }
        }
        
        private IEnumerable<CloudBlockBlob> GetAllBlobs()
        {        
            BlobContinuationToken blobContinuationToken = null;
            do
            {
                BlobResultSegment results = this.cloudBlobContainer.ListBlobsSegmented(
                    null,
                    true,
                    BlobListingDetails.None,
                    null,
                    blobContinuationToken,
                    null,
                    null);
                
                blobContinuationToken = results.ContinuationToken;
                foreach (IListBlobItem item in results.Results)
                {
                    if (item is CloudBlockBlob cloudBlockBlob)
                    {
                        yield return cloudBlockBlob;
                    }
                }
            }
            while (blobContinuationToken != null);
        }
    }
}