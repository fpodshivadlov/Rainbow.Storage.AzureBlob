using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rainbow.Storage.AzureBlob.Manager;
using Rainbow.Storage.AzureBlob.Tests.Helpers;
using Xunit;

namespace Rainbow.Storage.AzureBlob.Tests.Manager
{
    public class BlobListCacheAzureManagerTests : AzureManagerTests
    {
        public BlobListCacheAzureManagerTests() : base(true)
        {
        }
    }
    
    public class NoBlobListCacheAzureManagerTests : AzureManagerTests
    {
        public NoBlobListCacheAzureManagerTests() : base(false)
        {
        }
    }
    
    public abstract class AzureManagerTests
    {
        private readonly AzureManager azureManager;

        protected AzureManagerTests(bool useBlobListCache)
        {
            string connectionString = ConfigHelpers.GetConnectionString();
            string containerName = ConfigHelpers.GetContainerName();
            
            this.azureManager = new AzureManager(connectionString, containerName, "/Testing", useBlobListCache);
        }

        [Fact]
        public void Ctor_CheckContainerNotExist()
        {
            string connectionString = ConfigHelpers.GetConnectionString();
            
            Assert.ThrowsAny<Exception>(
                () => new AzureManager(connectionString, "notexist", null, false));
        }
        
        [Fact]
        public void EnumerateFiles_AllDirectories()
        {
            IList<string> items = this.azureManager
                .EnumerateFiles("/Testing", ".yml", SearchOption.AllDirectories)
                .ToList();
            
            Assert.NotEmpty(items);
            Assert.All(items, x => x.EndsWith(".yml"));
            Assert.All(items, x => x.StartsWith("Testing/"));
            Assert.True(items.Count > 6, "Enough items should be returned");
        }
        
        [Fact]
        public void EnumerateFiles_TopDirectoryOnly()
        {
            ICollection<string> items = this.azureManager
                .EnumerateFiles("/Testing/media/Imported", "yml", SearchOption.TopDirectoryOnly)
                .ToList();
            
            Assert.Equal(1, items.Count);
        }
        
        [Fact]
        public void FileExists()
        {
            bool exists = this.azureManager.FileExists("/Testing/media/Imported/Imported.yml");
            Assert.True(exists);
            
            bool shouldNotExists = this.azureManager.FileExists("/Testing/media/Imported/NotImported.yml");
            Assert.False(shouldNotExists);
        }
        
        [Fact]
        public void DirectoryExists()
        {
            Assert.True(this.azureManager.DirectoryExists("/Testing/media/Imported/"));
            Assert.False(this.azureManager.DirectoryExists("/Testing/media/NotImported/"));
            
            Assert.True(this.azureManager.DirectoryExists("/Testing/media/"));
            Assert.True(this.azureManager.DirectoryExists("/Testing"));
            Assert.False(this.azureManager.DirectoryExists("/NotTesting"));
            
            Assert.True(this.azureManager.DirectoryExists("/"));
        }
    }
}