using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rainbow.Storage.AzureBlob.Provider;
using Rainbow.Storage.AzureBlob.Tests.Utils;
using Xunit;

namespace Rainbow.Storage.AzureBlob.Tests
{
    public class AzureProviderTests
    {
        private readonly AzureProvider azureProvider;

        public AzureProviderTests()
        {
            string connectionString = Helpers.GetConnectionString();
            string containerName = Helpers.GetContainerName();
            
            this.azureProvider = new AzureProvider(connectionString, containerName);
        }

        [Fact]
        public void Ctor_CheckContainerNotExist()
        {
            string connectionString = Helpers.GetConnectionString();
            
            Assert.ThrowsAny<Exception>(() => new AzureProvider(connectionString, "notexist"));
        }
        
        [Fact]
        public void EnumerateFiles_AllDirectories()
        {
            IList<string> items = this.azureProvider
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
            ICollection<string> items = this.azureProvider
                .EnumerateFiles("/Testing/media/Imported", "yml", SearchOption.TopDirectoryOnly)
                .ToList();
            
            Assert.Equal(1, items.Count);
        }
        
        [Fact]
        public void FileExists()
        {
            bool exists = this.azureProvider.FileExists("/Testing/media/Imported/Imported.yml");
            Assert.True(exists);
            
            bool shouldNotExists = this.azureProvider.FileExists("/Testing/media/Imported/NotImported.yml");
            Assert.False(shouldNotExists);
        }
        
        [Fact]
        public void DirectoryExists()
        {
            Assert.True(this.azureProvider.DirectoryExists("/Testing/media/Imported/"));
            Assert.False(this.azureProvider.DirectoryExists("/Testing/media/NotImported/"));
            
            Assert.True(this.azureProvider.DirectoryExists("/Testing/media/"));
            Assert.True(this.azureProvider.DirectoryExists("/Testing"));
            Assert.False(this.azureProvider.DirectoryExists("/NotTesting"));
            
            Assert.True(this.azureProvider.DirectoryExists("/"));
        }
    }
}