using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Rainbow.Storage.AzureBlob.Provider;
using Rainbow.Storage.AzureBlob.Utils;
using Sitecore.Diagnostics;

namespace Rainbow.Storage.AzureBlob.Manager
{
    public class AzureManager
    {
        private readonly CloudBlobContainer cloudBlobContainer;
        private readonly IAzureProvider azureProvider;

        public AzureManager(string storageConnectionString, string containerName, bool useBlobListCache)
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
            this.azureProvider = useBlobListCache
                ? (IAzureProvider) new CachedAzureProvider(cloudBlobContainer)
                : new AzureProvider(cloudBlobContainer);
        }

        public IEnumerable<string> EnumerateFiles(string path, string extension, SearchOption searchOption)
        {
            Assert.ArgumentNotNullOrEmpty(path, nameof(path));

            extension = extension.Trim('.');
            string prefix = AzureUtils.DirectoryPathToPrefix(path);

            IEnumerable<IListBlobItem> items = this.azureProvider.EnumerateBlobs(prefix, searchOption);
            foreach (IListBlobItem item in items)
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
        
        public bool FileExists(string filePath)
        {
            string name = AzureUtils.FilePathToName(filePath);

            return this.azureProvider.BlobExists(name);
        }

        public bool DirectoryExists(string directoryPath)
        {
            string prefix = AzureUtils.DirectoryPathToPrefix(directoryPath);

            IEnumerable<IListBlobItem> list = this.azureProvider.EnumerateBlobs(
                prefix,
                SearchOption.AllDirectories,
                4);

            return list.OfType<CloudBlockBlob>().Any();
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