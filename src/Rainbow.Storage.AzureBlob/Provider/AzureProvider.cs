using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Sitecore.Diagnostics;

namespace Rainbow.Storage.AzureBlob.Provider
{
    public class AzureProvider
    {
        private readonly CloudBlobContainer cloudBlobContainer;

        public AzureProvider(string storageConnectionString, string containerName)
        {
            Assert.ArgumentNotNullOrEmpty(storageConnectionString, nameof(storageConnectionString));

            if (!CloudStorageAccount.TryParse(storageConnectionString, out CloudStorageAccount storageAccount))
            {
                throw new ArgumentException($"{nameof(storageConnectionString)} is not a valid connection string");
            }
            
            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);
            if (!cloudBlobContainer.Exists())
            {
                throw new DataException($"${containerName} is not found");
            }

            this.cloudBlobContainer = cloudBlobContainer;
        }

        public IEnumerable<string> EnumerateFiles(string path, string extension, SearchOption searchOption)
        {
            Assert.ArgumentNotNullOrEmpty(path, nameof(path));

            extension = extension.Trim('.');
            string prefix = AzureUtils.DirectoryPathToPrefix(path);
            BlobContinuationToken blobContinuationToken = null;
            do
            {
                var results = cloudBlobContainer.ListBlobsSegmented(
                    prefix,
                    searchOption == SearchOption.AllDirectories,
                    BlobListingDetails.None,
                    null,
                    blobContinuationToken,
                    new BlobRequestOptions(),
                    null
                );
                
                blobContinuationToken = results.ContinuationToken;
                foreach (IListBlobItem item in results.Results)
                {
                    if (item is CloudBlockBlob cloudBlockBlob)
                    {
                        var name = cloudBlockBlob.Name;
                        
                        if (string.IsNullOrEmpty(prefix) || name.StartsWith(prefix))
                        {
                            if (string.IsNullOrEmpty(extension) || name.EndsWith($".{extension}"))
                            {
                                yield return cloudBlockBlob.Name;
                            }
                        }
                    }
                }
            }
            while (blobContinuationToken != null);
        }
        
        public bool FileExists(string filePath)
        {
            string name = AzureUtils.FilePathToName(filePath);
            CloudBlob blobReference = this.cloudBlobContainer.GetBlobReference(name);
            
            return blobReference.Exists();
        }

        public bool DirectoryExists(string directoryPath)
        {
            string prefix = AzureUtils.DirectoryPathToPrefix(directoryPath);
            CloudBlobDirectory blobDirectory = this.cloudBlobContainer.GetDirectoryReference(prefix);
            
            BlobContinuationToken blobContinuationToken = null;
            do
            {
                BlobResultSegment results = blobDirectory.ListBlobsSegmented(
                    true,
                    BlobListingDetails.None,
                    4,
                    blobContinuationToken,
                    null, 
                    null
                );
                
                blobContinuationToken = results.ContinuationToken;
                if (results.Results.OfType<CloudBlockBlob>().Any())
                {
                    return true;
                }
            }
            while (blobContinuationToken != null);

            return false;
        }

        public Stream GetFileStream(string filePath, bool openRead = true)
        {
            string name = AzureUtils.FilePathToName(filePath);

            CloudBlockBlob cloudBlockBlob = this.cloudBlobContainer.GetBlockBlobReference(name);

            return openRead ? cloudBlockBlob.OpenRead() : cloudBlockBlob.OpenWrite();
        }

        public void EnsureDirectory(string physicalRootPath)
        {
            // ToDo: it's not supported by Azure
        }
    }
}