using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Rainbow.Formatting;
using Rainbow.Model;
using Rainbow.Settings;
using Rainbow.Storage.AzureBlob.Manager;
using Rainbow.Storage.AzureBlob.Provider;
using Sitecore.Common;
using Sitecore.Diagnostics;
using Sitecore.StringExtensions;

namespace Rainbow.Storage.AzureBlob
{
    public sealed class SerializationBlobStorageTree : IDisposable
    {
        private static readonly HashSet<string> InvalidFileNames =
            new HashSet<string>(RainbowSettings.Current.SfsInvalidFilenames, StringComparer.OrdinalIgnoreCase);
        private readonly char[] _invalidFileNameCharacters = Path.GetInvalidFileNameChars()
            .Concat(RainbowSettings.Current.SfsExtraInvalidFilenameCharacters).ToArray();
        private readonly ConcurrentDictionary<Guid, IItemMetadata> _idCache =
            new ConcurrentDictionary<Guid, IItemMetadata>();
        private readonly object _fastReadConfigurationLock = new object();
        
        private readonly AzureBlobCache<IItemMetadata> _metadataCache;
        private readonly string _globalRootItemPath;
        private readonly string _physicalRootPath;
        private readonly ISerializationFormatter _formatter;
        private readonly AzureManager azureManager;
        private readonly AzureBlobCache<IItemData> _dataCache;
        private bool _configuredForFastReads;
        private int? _maxRelativePathLength;
        private int? _maxItemNameLength;

        public SerializationBlobStorageTree(
            string name,
            string globalRootItemPath,
            string databaseName,
            string physicalRootPath,
            ISerializationFormatter formatter,
            bool useDataCache,
            AzureManager azureManager)
        {
            Assert.ArgumentNotNullOrEmpty(globalRootItemPath, nameof(globalRootItemPath));
            Assert.ArgumentNotNullOrEmpty(databaseName, nameof(databaseName));
            Assert.ArgumentNotNullOrEmpty(physicalRootPath, nameof(physicalRootPath));
            Assert.ArgumentNotNull(formatter, nameof(formatter));
            Assert.ArgumentNotNull(azureManager, nameof(azureManager));
            Assert.IsTrue(globalRootItemPath.StartsWith("/"),
                "The global root item path must start with '/', e.g. '/sitecore' or '/sitecore/content'");
            Assert.IsTrue(globalRootItemPath.Length > 1,
                "The global root item path cannot be '/' - there is no root item. You probably mean '/sitecore'.");

            this._globalRootItemPath = globalRootItemPath.TrimEnd('/');

            this.AssertValidPhysicalPath(physicalRootPath);
            this._physicalRootPath = physicalRootPath;
            this.azureManager = azureManager;
            this._formatter = formatter;
            this._dataCache = new AzureBlobCache<IItemData>(azureManager, useDataCache);
            this._metadataCache = new AzureBlobCache<IItemMetadata>(azureManager, true);
            
            this.Name = name;
            this.DatabaseName = databaseName;

            this.azureManager.EnsureDirectory(this._physicalRootPath);
        }

        public string DatabaseName { get; }

        public string GlobalRootItemPath => this._globalRootItemPath;

        public string PhysicalRootPath => this._physicalRootPath;

        [ExcludeFromCodeCoverage] public string Name { get; private set; }

        public IEnumerable<IItemData> GetSnapshot()
        {
            return this.azureManager
                .EnumerateFiles(this._physicalRootPath, this._formatter.FileExtension, SearchOption.AllDirectories)
                .Select(this.ReadItem);
        }

        public bool ContainsPath(string globalPath)
        {
            if (!globalPath.EndsWith("/"))
                globalPath += "/";

            return globalPath.StartsWith(this._globalRootItemPath + "/", StringComparison.OrdinalIgnoreCase);
        }

        public IItemData GetRootItem()
        {
            string[] files = this.azureManager
                .EnumerateFiles(this._physicalRootPath, this._formatter.FileExtension, SearchOption.TopDirectoryOnly)
                .ToArray();

            if (files.Length == 0)
                return null;

            if (files.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Found multiple root items in {this._physicalRootPath}! This is not valid: a tree may only have one root node.");
            }

            return this.ReadItem(files.First());
        }

        public IEnumerable<IItemData> GetItemsByPath(string globalPath)
        {
            Assert.ArgumentNotNullOrEmpty(globalPath, nameof(globalPath));

            return this.GetPhysicalFilePathsForVirtualPath(this.ConvertGlobalVirtualPathToTreeVirtualPath(globalPath))
                .Select(this.ReadItem)
                .Where(item =>
                {
                    if (item != null)
                        return item.Path.Equals(globalPath, StringComparison.OrdinalIgnoreCase);

                    return false;
                });
        }

        public IItemData GetItemById(Guid id)
        {
            this.EnsureConfiguredForFastReads();

            IItemMetadata fromMetadataCache = this.GetFromMetadataCache(id);
            if (fromMetadataCache == null)
                return null;

            return this.ReadItem(fromMetadataCache.SerializedItemId);
        }

        public IEnumerable<IItemData> GetChildren(IItemMetadata parentItem)
        {
            Assert.ArgumentNotNull(parentItem, nameof(parentItem));

            return this.GetChildPaths(parentItem).AsParallel().Select(this.ReadItem);
        }

        public IEnumerable<IItemMetadata> GetChildrenMetadata(IItemMetadata parentItem)
        {
            Assert.ArgumentNotNull(parentItem, nameof(parentItem));

            return this.GetChildPaths(parentItem).AsParallel().Select(this.ReadItemMetadata);
        }

        private IItemData ReadItem(string filePath)
        {
            Assert.ArgumentNotNullOrEmpty(filePath, nameof(filePath));
            
            IItemData item = this._dataCache.GetValue(filePath, name =>
            {
                try
                {
                    using (Stream stream = this.azureManager.GetFileStream(name))
                    {
                        IItemData itemData = this._formatter.ReadSerializedItem(stream, filePath);
                        itemData.DatabaseName = this.DatabaseName;
                        this.AddToMetadataCache(itemData);
                        Log.Info($"AZURE BLOB STORAGE: reading {filePath} data. Stopped at position {stream?.Position}", this);
                        
                        return itemData;
                    }
                }
                catch (Exception ex)
                {
                    throw new SfsReadException($"AZURE BLOB STORAGE: Error while reading SFS item {filePath}", ex);
                }
            });

            if (this._dataCache.Enabled && item != null)
                return new FsCachedItem(item, () => this.GetChildren(item));

            return item;
        }

        private IItemMetadata ReadItemMetadata(string filePath)
        {
            Assert.ArgumentNotNullOrEmpty(filePath, nameof(filePath));
            
            return this._metadataCache.GetValue(filePath, fileInfo =>
            {
                try
                {
                    using (Stream stream = this.azureManager.GetFileStream(filePath))
                    {
                        IItemMetadata itemMetadata = this._formatter.ReadSerializedItemMetadata(stream, filePath);
                        this._idCache[itemMetadata.Id] = itemMetadata;
                        Log.Info($"AZURE BLOB STORAGE: reading {filePath} metadata. Stopped at position {stream?.Position}", this);
                        
                        return itemMetadata;
                    }
                }
                catch (Exception ex)
                {
                    throw new SfsReadException($"AZURE BLOB STORAGE: Error while reading SFS metadata {filePath}", ex);
                }
            });
        }

        private string ConvertGlobalVirtualPathToTreeVirtualPath(string globalPath)
        {
            Assert.ArgumentNotNullOrEmpty(globalPath, nameof(globalPath));

            if (!globalPath.StartsWith(this._globalRootItemPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"AZURE BLOB STORAGE: The global path {globalPath} was not rooted " +
                    $"under the local item root path {this._globalRootItemPath}. " +
                    "This means you tried to put an item where it didn't belong.");

            int startIndex = this._globalRootItemPath.LastIndexOf('/');
            return globalPath.Substring(startIndex);
        }

        private string[] GetPhysicalFilePathsForVirtualPath(string virtualPath)
        {
            Assert.ArgumentNotNullOrEmpty(virtualPath, nameof(virtualPath));

            string[] pathComponents = virtualPath
                .Trim('/')
                .Split('/')
                .Select(this.PrepareItemNameForFileSystem)
                .ToArray();

            var parentPaths = new List<string>
            {
                Path.Combine(this._physicalRootPath, pathComponents.First() + this._formatter.FileExtension)
            };

            foreach (string pathComponent in pathComponents.Skip(1))
            {
                string[] startingParentPathsArray = parentPaths.ToArray();
                parentPaths.Clear();

                foreach (string path in startingParentPathsArray)
                {
                    if (this.azureManager.FileExists(path))
                    {
                        parentPaths.AddRange(this.GetChildPaths(this.ReadItemMetadata(path))
                            .Where(childPath => (Path.GetFileName(childPath) ?? "")
                                .StartsWith(pathComponent, StringComparison.OrdinalIgnoreCase)));
                    }
                }
            }

            return parentPaths.ToArray();
        }

        private string[] GetChildPaths(IItemMetadata item)
        {
            Assert.ArgumentNotNull(item, nameof(item));

            IItemMetadata serializedItem = this.GetItemForGlobalPath(item.Path, item.Id);
            if (serializedItem == null)
                throw new InvalidOperationException($"Item {item.Path} does not exist on disk.");

            IEnumerable<string> childPaths = Enumerable.Empty<string>();
            string childrenPath = Path.ChangeExtension(serializedItem.SerializedItemId, null);

            if (this.azureManager.DirectoryExists(childrenPath))
            {
                childPaths = this.azureManager.EnumerateFiles(
                    childrenPath,
                    this._formatter.FileExtension,
                    SearchOption.TopDirectoryOnly);
            }

            string guidBasedPath = Path.Combine(this._physicalRootPath, item.Id.ToString());
            if (this.azureManager.DirectoryExists(guidBasedPath))
            {
                childPaths = childPaths.Concat(this.azureManager.EnumerateFiles(
                    guidBasedPath, 
                    this._formatter.FileExtension, 
                    SearchOption.TopDirectoryOnly));
            }

            string[] result = childPaths.ToArray();
            return result;
        }

        private string PrepareItemNameForFileSystem(string name)
        {
            Assert.ArgumentNotNullOrEmpty(name, nameof(name));
            
            string validatedName = string.Join("_", name.TrimStart(' ').Split(this._invalidFileNameCharacters));
    
            if (validatedName.Length > this.MaxItemNameLengthBeforeTruncation)
                    validatedName = validatedName.Substring(0, this.MaxItemNameLengthBeforeTruncation);
    
            // if the name ends with a space that can cause ambiguous results (e.g. "Multilist" and "Multilist ");
            // Win32 considers directories with trailing spaces as the same as without, so we end it with underscore instead
            if (validatedName[validatedName.Length - 1] == ' ')
                validatedName = validatedName.Substring(0, validatedName.Length - 1) + "_";
    
            if (SerializationBlobStorageTree.InvalidFileNames.Contains(validatedName))
                return "_" + validatedName;
    
            return validatedName;
        }

        private void AssertValidPhysicalPath(string physicalPath)
        {
            string str = physicalPath;
            var chArray = new[]
            {
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            };

            foreach (string source in str.Split(chArray))
            {
                if (SerializationBlobStorageTree.InvalidFileNames.Contains(source))
                    throw new ArgumentException(
                        $"Illegal file or directory name {source} is part of the tree root physical path {physicalPath}. If you're using Unicorn, you may need to specify a 'name' attribute on your include to make the path a valid name.",
                        nameof(physicalPath));

                foreach (char fileNameCharacter in this._invalidFileNameCharacters)
                {
                    if (source.Contains(fileNameCharacter) && (fileNameCharacter != ':' || source.IndexOf(':') != 1))
                        throw new ArgumentException(
                            $"Illegal character {fileNameCharacter} in tree root physical path {physicalPath}. If you're using Unicorn, you may need to specify a 'name' attribute on your include to make the path a valid name.",
                            nameof(physicalPath));
                }
            }
        }

        private IItemMetadata GetItemForGlobalPath(string globalPath, Guid expectedItemId)
        {
            Assert.ArgumentNotNullOrEmpty(globalPath, nameof(globalPath));

            string localPath = this.ConvertGlobalVirtualPathToTreeVirtualPath(globalPath);

            IItemMetadata cached = this.GetFromMetadataCache(expectedItemId);
            if (cached != null && globalPath.Equals(cached.Path, StringComparison.OrdinalIgnoreCase))
                return cached;

            IItemMetadata result = this.GetPhysicalFilePathsForVirtualPath(localPath)
                .Select(this.ReadItemMetadata)
                .FirstOrDefault(candidateItem => candidateItem != null && candidateItem.Id == expectedItemId);

            if (result == null)
                return null;

            // in a specific circumstance we want to ignore dupe items with the same IDs:
            // when we move or rename an item we delete the old items after we wrote the newly moved/renamed items
            // this means that the tree temporarily has known dupes. We need to be able to ignore those
            // when we're deleting the old items to make the tree sane again.
            if (Switcher<bool, SfsDuplicateIdCheckingDisabler>.CurrentValue)
            {
                return result;
            }

            IItemMetadata temp = this.GetFromMetadataCache(expectedItemId);
            if (temp != null && temp.SerializedItemId != result.SerializedItemId)
                throw new InvalidOperationException("The item with ID {0} has duplicate item files serialized ({1}, {2}). Please remove the incorrect one and try again.".FormatWith(result.Id, temp.SerializedItemId, result.SerializedItemId));

            // note: we only actually add to cache if we checked for dupe IDs. This is to avoid cache poisoning.
            this.AddToMetadataCache(result);

            return result;
        }

        private IList<IItemMetadata> GetDescendants(
            IItemMetadata root,
            bool ignoreReadErrors)
        {
            Assert.ArgumentNotNull(root, nameof(root));

            IList<IItemMetadata> itemMetadataList = new List<IItemMetadata>();
            Queue<IItemMetadata> itemMetadataQueue = new Queue<IItemMetadata>();
            itemMetadataQueue.Enqueue(root);
            while (itemMetadataQueue.Count > 0)
            {
                IItemMetadata processingItemMetadata = itemMetadataQueue.Dequeue();
                if (processingItemMetadata.Id != root.Id)
                    itemMetadataList.Add(processingItemMetadata);

                foreach (string childPath in this.GetChildPaths(processingItemMetadata))
                {
                    try
                    {
                        IItemMetadata childItemMetadata = this.ReadItemMetadata(childPath);
                        if (childItemMetadata != null)
                            itemMetadataQueue.Enqueue(childItemMetadata);
                    }
                    catch (Exception)
                    {
                        if (!ignoreReadErrors)
                            throw;
                    }
                }
            }

            return itemMetadataList;
        }

        private int MaxRelativePathLength
        {
            get
            {
                if (!this._maxRelativePathLength.HasValue)
                {
                    int folderPathMaxLength = RainbowSettings.Current.SfsSerializationFolderPathMaxLength;
                    if (this._physicalRootPath.Length > folderPathMaxLength)
                    {
                        throw new InvalidOperationException(
                            $"The physical root path of this SFS tree, {this._physicalRootPath}, " +
                            $"is longer than the configured max base path length {folderPathMaxLength}. " +
                            $"If the tree contains any loopback paths, unexpected behavior may occur. " +
                            $"You should increase the Rainbow.SFS.SerializationFolderPathMaxLength setting " +
                            $"in Rainbow.config to greater than {this._physicalRootPath.Length} " +
                            $"and perform a reserialization from a master content database.");
                    }

                    this._maxRelativePathLength = 240 - folderPathMaxLength;
                }

                return this._maxRelativePathLength.Value;
            }
        }

        private int MaxItemNameLengthBeforeTruncation
        {
            get
            {
                if (!this._maxItemNameLength.HasValue)
                {
                    int beforeTruncation = RainbowSettings.Current.SfsMaxItemNameLengthBeforeTruncation;
                    int num = this.MaxRelativePathLength - Guid.Empty.ToString().Length -
                              this._formatter.FileExtension.Length;

                    if (beforeTruncation > num)
                        throw new InvalidOperationException(
                            "The MaxItemNameLengthBeforeTruncation setting ({0}) is too long given the SerializationFolderPathMaxLength. Reduce the max name length to at or below {1}."
                                .FormatWith((object) beforeTruncation, (object) num));

                    this._maxItemNameLength = beforeTruncation;
                }

                return this._maxItemNameLength.Value;
            }
        }

        private void AddToMetadataCache(IItemMetadata metadata, string path = null)
        {
            var writtenItemMetadata = new WrittenItemMetadata(metadata.Id, metadata.ParentId, metadata.TemplateId,
                metadata.Path, path ?? metadata.SerializedItemId);
            this._idCache[metadata.Id] = writtenItemMetadata;
            this._metadataCache.AddOrUpdate(writtenItemMetadata.SerializedItemId, writtenItemMetadata);
        }

        private IItemMetadata GetFromMetadataCache(Guid itemId)
        {
            if (this._idCache.TryGetValue(itemId, out IItemMetadata itemMetadata) &&
                this.azureManager.FileExists(itemMetadata.SerializedItemId))
                return itemMetadata;

            return null;
        }

        private void EnsureConfiguredForFastReads()
        {
            if (this._configuredForFastReads)
                return;

            lock (this._fastReadConfigurationLock)
            {
                if (this._configuredForFastReads)
                    return;

                IItemData rootItem = this.GetRootItem();
                if (rootItem != null)
                    this.GetDescendants(rootItem, false);

                this._configuredForFastReads = true;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            // ToDo: 
            if (!disposing)
                return;
        }

        [DebuggerDisplay("{Id} {Path} [Metadata - {SerializedItemId}]")]
        private class WrittenItemMetadata : IItemMetadata
        {
            public WrittenItemMetadata(
                Guid id,
                Guid parentId,
                Guid templateId,
                string path,
                string serializedItemId)
            {
                this.Id = id;
                this.ParentId = parentId;
                this.TemplateId = templateId;
                this.Path = path;
                this.SerializedItemId = serializedItemId;
            }

            public Guid Id { get; }

            public Guid ParentId { get; }

            public Guid TemplateId { get; }

            public string Path { get; }

            public string SerializedItemId { get; }
        }
    }
}