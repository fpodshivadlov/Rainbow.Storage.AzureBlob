using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Rainbow.Filtering;
using Rainbow.Model;
using Rainbow.Storage.AzureBlob.Tests.Fakes;
using Rainbow.Storage.AzureBlob.Tests.Utils;
using Rainbow.Storage.Yaml;
using Xunit;

namespace Rainbow.Storage.AzureBlob.Tests
{
    public class SerializationBlobStorageDataStoreTests
    {       
        [Fact]
        public void GetSnapshot()
        {
            using (SerializationBlobStorageDataStore dataStore = this.CreateDataStore())
            {
                List<IItemData> items = dataStore.GetSnapshot().ToList();
            
                Assert.NotEmpty(items);
                Assert.Equal(3, items.Count);
            }
        }
        
        [Theory]
        [InlineData("59e2ed33-f81a-40be-af35-52457d611de5", "Imported")]
        [InlineData("f38af8f7-3756-4afd-aced-73866e71c338", "By Type")]
        public void GetById(string guid, string path)
        {
            using (SerializationBlobStorageDataStore dataStore = this.CreateDataStore())
            {
                IItemData item = dataStore.GetById(Guid.Parse(guid), "master");
            
                Assert.NotNull(item);
                Assert.Equal(path, item.Name);
            }
        }
        
        [Theory]
        [InlineData("59e2ed33-f81a-40be-af35-52457d611de5", "/sitecore/media library/Imported")]
        [InlineData("f38af8f7-3756-4afd-aced-73866e71c338", "/sitecore/media library/Imported/By Type")]
        public void GetByPathAndId(string guid, string path)
        {
            using (SerializationBlobStorageDataStore dataStore = this.CreateDataStore())
            {
                IItemData item = dataStore.GetByPathAndId(path, Guid.Parse(guid), "master");
            
                Assert.NotNull(item);
                Assert.Equal(path, item.Path);
            }
        }
        
        [Theory]
        [InlineData("59e2ed33-f81a-40be-af35-52457d611de5", "/sitecore/media library/Imported")]
        public void GetChildren(string guid, string path)
        {
            using (SerializationBlobStorageDataStore dataStore = this.CreateDataStore())
            {
                IItemData parentItem = dataStore.GetById(Guid.Parse(guid), "master");
                IEnumerable<IItemData> items = dataStore.GetChildren(parentItem);
            
                Assert.NotNull(items);
                Assert.Equal(2, items.Count());
            }
        }
        
        [Theory]
        [InlineData("59e2ed33-f81a-40be-af35-52457d611de5", "/sitecore/media library/Imported")]
        [InlineData("f38af8f7-3756-4afd-aced-73866e71c338", "/sitecore/media library/Imported/By Type")]
        public void GetByPath(string guid, string path)
        {
            using (SerializationBlobStorageDataStore dataStore = this.CreateDataStore())
            {
                IEnumerable<IItemData> items = dataStore.GetByPath(path, "master");
            
                Assert.NotNull(items);
                Assert.Equal(1, items.Count());
            }
        }
        
        [Fact]
        public void GetMetadataByTemplateId()
        {
            Guid folderTemplateGuid = Guid.Parse("fe5dd826-48c6-436d-b87a-7c4210c7413b");
            
            using (SerializationBlobStorageDataStore dataStore = this.CreateDataStore())
            {
                IEnumerable<IItemMetadata> items = dataStore
                    .GetMetadataByTemplateId(folderTemplateGuid, "master")
                    .ToArray();
            
                Assert.NotNull(items);
                Assert.Equal(3, items.Count());
                
                Assert.Contains(items, x => x.Path == "/sitecore/media library/Imported");
                Assert.Contains(items, x => x.Path == "/sitecore/media library/Imported/By Type");
            }
        }
        
        private SerializationBlobStorageDataStore CreateDataStore()
        {
            XmlElement formatterConfig = new XmlDocument().CreateElement("serializationFormatter");
            XmlElement fieldFilterConfig = new XmlDocument().CreateElement("fieldFilter");
            
            return new SerializationBlobStorageDataStore(
                "/Testing/media",
                true,
                Helpers.GetConnectionString(),
                Helpers.GetContainerName(),
                StaticTreeRootFactory.Create(
                    new TreeRoot("Imported", "/sitecore/media library/Imported", "master")
                ),
                new YamlSerializationFormatter(formatterConfig, new ConfigurationFieldFilter(fieldFilterConfig))
            );
        }
    }
}