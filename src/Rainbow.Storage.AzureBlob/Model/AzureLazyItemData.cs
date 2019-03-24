using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Rainbow.Formatting;
using Rainbow.Model;
using Rainbow.Storage.AzureBlob.Manager;
using Rainbow.Storage.AzureBlob.Utils;
using Rainbow.Storage.Yaml;
using Sitecore.Diagnostics;

namespace Rainbow.Storage.AzureBlob.Model
{
    public class AzureLazyItemData : IItemData
    {
        private readonly IItemMetadata itemMetadata;
        private readonly string filePath;
        private readonly IAzureManager azureManager;
        private readonly ISerializationFormatter serializationFormatter;
        private readonly IDataStore parentDataStore;
        
        private readonly Lazy<IItemData> itemDataLazy;
        
        private string _databaseName;

        public AzureLazyItemData(
            IItemMetadata itemMetadata,
            string filePath,
            IAzureManager azureManager,
            ISerializationFormatter serializationFormatter)
        {
            Assert.ArgumentNotNull(itemMetadata, nameof(itemMetadata));
            Assert.ArgumentNotNull(azureManager, nameof(azureManager));
            Assert.ArgumentNotNull(filePath, nameof(filePath));
            Assert.ArgumentNotNull(serializationFormatter, nameof(serializationFormatter));

            this.itemMetadata = itemMetadata;
            this.azureManager = azureManager;
            this.filePath = filePath;
            this.serializationFormatter = serializationFormatter;

            // ToDo: fix it - to do the lazy read, the context is needed to be preserved.
            if (serializationFormatter is YamlSerializationFormatter yamlSerializationFormatter)
            {
                this.parentDataStore = yamlSerializationFormatter.ParentDataStore;
            }
            
            this.itemDataLazy = new Lazy<IItemData>(this.LoadItemData, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public Guid Id => this.itemMetadata.Id;
        public Guid ParentId => this.itemMetadata.ParentId;
        public Guid TemplateId => this.itemMetadata.TemplateId;
        public string Path => this.itemMetadata.Path;
        public string SerializedItemId => this.itemMetadata.SerializedItemId;

        public string DatabaseName {
            get => this._databaseName ?? this.itemDataLazy.Value.DatabaseName;
            set => this._databaseName = value;
        }

        public IEnumerable<IItemData> GetChildren()
        {
            // ToDo: improve
            if (this.parentDataStore != null)
                return this.parentDataStore.GetChildren(this);

            return this.itemDataLazy.Value.GetChildren();
        }

        public string Name => this.itemDataLazy.Value.Name;
        public Guid BranchId => this.itemDataLazy.Value.BranchId;
        public IEnumerable<IItemFieldValue> SharedFields => this.itemDataLazy.Value.SharedFields;
        public IEnumerable<IItemLanguage> UnversionedFields => this.itemDataLazy.Value.UnversionedFields;
        public IEnumerable<IItemVersion> Versions => this.itemDataLazy.Value.Versions;
              
        private IItemData LoadItemData()
        {
            try
            {
                using (Stream stream = this.azureManager.GetFileStream(this.filePath))
                {
                    IItemData itemData = this.serializationFormatter.ReadSerializedItem(stream, this.filePath);
                    itemData.DatabaseName = this._databaseName;
                    
                    return itemData;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Rainbow] [AzureBlob] Error during lazy data loading of {this.filePath}.", ex, typeof(AzureLazyItemData));
                throw new SfsReadException($"[Rainbow] [AzureBlob] Error while lazy reading SFS item {this.filePath}", ex);
            }
        }
    }
}