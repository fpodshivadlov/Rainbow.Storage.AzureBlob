using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rainbow.Formatting;
using Rainbow.Model;
using Rainbow.Storage.AzureBlob.Manager;
using Rainbow.Storage.AzureBlob.Provider;
using Sitecore.Diagnostics;

namespace Rainbow.Storage.AzureBlob
{
	public class SerializationBlobStorageDataStore : ISnapshotCapableDataStore, IDocumentable, IDisposable
	{
		private readonly string CloudRootPath;
		private readonly bool _useDataCache;
		private readonly ITreeRootFactory _rootFactory;
		private readonly IList<SerializationBlobStorageTree> Trees;
		private readonly ISerializationFormatter _formatter;
		private readonly AzureManager azureManager;
		private readonly string _containerName;

		public SerializationBlobStorageDataStore(
			string cloudRootPath,
			bool useDataCache,
			bool useBlobListCache,
			string connectionString,
			string containerName,
			ITreeRootFactory rootFactory,
			ISerializationFormatter formatter)
		{
			Assert.ArgumentNotNullOrEmpty(cloudRootPath, nameof(cloudRootPath));
			Assert.ArgumentNotNull(formatter, nameof(formatter));
			Assert.ArgumentNotNull(rootFactory, nameof(rootFactory));

			this.azureManager = new AzureManager(connectionString, containerName, useBlobListCache);
			this._containerName = containerName;
			this._useDataCache = useDataCache;
			this._rootFactory = rootFactory;
			this._formatter = formatter;
			this._formatter.ParentDataStore = this;
		
			// ReSharper disable once DoNotCallOverridableMethodsInConstructor
			this.CloudRootPath = this.InitializeRootPath(cloudRootPath);

			// ReSharper disable once DoNotCallOverridableMethodsInConstructor
			this.Trees = this.InitializeTrees(this._formatter, useDataCache);
		}

		public virtual IEnumerable<IItemData> GetSnapshot()
		{
			return this.Trees.SelectMany(tree => tree.GetSnapshot());
		}

		public virtual void Save(IItemData item)
		{
			throw new NotImplementedException("Editing is not supported.");
		}

		public virtual void MoveOrRenameItem(IItemData itemWithFinalPath, string oldPath)
		{
			throw new NotImplementedException("Changes are not supported.");
		}

		public virtual IEnumerable<IItemData> GetByPath(string path, string database)
		{
			SerializationBlobStorageTree tree = this.GetTreeForPath(path, database);

			if (tree == null)
			{
				return Enumerable.Empty<IItemData>();
			}

			return tree.GetItemsByPath(path);
		}

		public virtual IItemData GetByPathAndId(string path, Guid id, string database)
		{
			Assert.ArgumentNotNullOrEmpty(path, "path");
			Assert.ArgumentNotNullOrEmpty(database, "database");
			Assert.ArgumentCondition(id != default(Guid), "id", "The item ID must not be the null guid. Use GetByPath() if you don't know the ID.");

			IItemData[] items = this.GetByPath(path, database).ToArray();

			return items.FirstOrDefault(item => item.Id == id);
		}

		public virtual IItemData GetById(Guid id, string database)
		{
			foreach (SerializationBlobStorageTree tree in this.Trees)
			{
				IItemData result = tree.GetItemById(id);

				if (result != null && result.DatabaseName.Equals(database))
				{
					return result;
				}
			}

			return null;
		}

		public virtual IEnumerable<IItemMetadata> GetMetadataByTemplateId(Guid templateId, string database)
		{
			return this.Trees.Select(tree => tree.GetRootItem())
				.Where(root => root != null)
				.AsParallel()
				.SelectMany(tree => this.FilterDescendantsAndSelf(tree, item => item.TemplateId == templateId));
		}

		public virtual IEnumerable<IItemData> GetChildren(IItemData parentItem)
		{
			SerializationBlobStorageTree tree = this.GetTreeForPath(parentItem.Path, parentItem.DatabaseName);

			if (tree == null) throw new InvalidOperationException("No trees contained the global path " + parentItem.Path);

			return tree.GetChildren(parentItem);
		}

		public virtual void CheckConsistency(string database, bool fixErrors, Action<string> logMessageReceiver)
		{
			// TODO: consistency check
			throw new NotImplementedException();
		}

		public virtual void ResetTemplateEngine()
		{
			// do nothing, the YAML serializer has no template engine
		}

		public virtual bool Remove(IItemData item)
		{
			// ToDo: changes are not supported.
			throw new NotImplementedException("Changes are not supported.");
		}

		public virtual void RegisterForChanges(Action<IItemMetadata, string> actionOnChange)
		{
			// ToDo: changes are not supported.
		}

		public virtual void Clear()
		{
			// ToDo: changes are not supported.
			throw new NotImplementedException("Changes are not supported.");
		}
		
		protected virtual string InitializeRootPath(string rootPath)
		{
			return rootPath;
		}

		protected virtual SerializationBlobStorageTree GetTreeForPath(string path, string database)
		{
			SerializationBlobStorageTree foundStorageTree = null;
			foreach (SerializationBlobStorageTree tree in this.Trees)
			{
				if (!tree.DatabaseName.Equals(database, StringComparison.OrdinalIgnoreCase)) continue;
				if (!tree.ContainsPath(path)) continue;

				if (foundStorageTree != null)
				{
					throw new InvalidOperationException(
						$"The trees {foundStorageTree.Name} and {tree.Name} both contained the global path {path}" +
						$" - overlapping trees are not allowed.");
				}

				foundStorageTree = tree;
			}

			return foundStorageTree;
		}

		// note: we pass in these params (formatter, datacache)
		// so that overriding classes may get access to private vars indirectly
		// (can't get at them otherwise because this is called from the constructor)
		protected virtual List<SerializationBlobStorageTree> InitializeTrees(ISerializationFormatter formatter, bool useDataCache)
		{
			var result = new List<SerializationBlobStorageTree>();
			IEnumerable<TreeRoot> roots = this._rootFactory.CreateTreeRoots();

			foreach (TreeRoot root in roots)
			{
				result.Add(this.CreateTree(root, formatter, useDataCache));
			}

			return result;
		}

		// note: we pass in these params (formatter, datacache) so that overriding classes may get access to private vars indirectly (can't get at them otherwise because this is called from the constructor)
		protected virtual SerializationBlobStorageTree CreateTree(TreeRoot root, ISerializationFormatter formatter, bool useDataCache)
		{
			var tree = new SerializationBlobStorageTree(
				root.Name, 
				root.Path, 
				root.DatabaseName,
				Path.Combine(this.CloudRootPath, root.Name),
				formatter,
				useDataCache,
				this.azureManager);

			return tree;
		}

		protected virtual IList<IItemMetadata> FilterDescendantsAndSelf(IItemData root, Func<IItemMetadata, bool> predicate)
		{
			Assert.ArgumentNotNull(root, nameof(root));

			var descendants = new List<IItemMetadata>();

			var childQueue = new Queue<IItemMetadata>();
			childQueue.Enqueue(root);
			while (childQueue.Count > 0)
			{
				IItemMetadata parent = childQueue.Dequeue();

				if (predicate(parent))
					descendants.Add(parent);

				SerializationBlobStorageTree tree = this.GetTreeForPath(parent.Path, root.DatabaseName);

				if (tree == null)
					continue;

				IItemMetadata[] children = tree.GetChildrenMetadata(parent).ToArray();

				foreach (IItemMetadata item in children)
					childQueue.Enqueue(item);
			}

			return descendants;
		}

		public virtual string FriendlyName => "Serialization Azure Blob Storage Data Store";
		
		public virtual string Description => "Stores serialized items on Azure Blob Storage using the SFS tree format, where each root is a separate tree.";

		public virtual KeyValuePair<string, string>[] GetConfigurationDetails()
		{
			return new[]
			{
				new KeyValuePair<string, string>("Serialization formatter", DocumentationUtility.GetFriendlyName(this._formatter)),
				new KeyValuePair<string, string>("Cloud root path", this.CloudRootPath),
				new KeyValuePair<string, string>("Azure BLOB container name", this._containerName), 
				new KeyValuePair<string, string>("Use data cache", this._useDataCache.ToString()), 
				new KeyValuePair<string, string>("Total internal SFS trees", this.Trees.Count.ToString())
			};
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				foreach (SerializationBlobStorageTree tree in this.Trees)
				{
					tree.Dispose();
				}
			}
		}
	}

}