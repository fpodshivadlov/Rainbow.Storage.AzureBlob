using System;
using System.Collections.Concurrent;
using System.IO;
using Rainbow.Storage.AzureBlob.Provider;
using Sitecore.Diagnostics;

namespace Rainbow.Storage.AzureBlob
{
   public sealed class AzureBlobCache<T> where T : class
  {
    private readonly ConcurrentDictionary<string, AzureBlobCacheEntry<T>> _cache = 
      new ConcurrentDictionary<string, AzureBlobCacheEntry<T>>(StringComparer.OrdinalIgnoreCase);
   

    private readonly AzureProvider azureProvider;
    
    public AzureBlobCache(AzureProvider azureProvider, bool enabled)
    {
      Assert.ArgumentNotNull(azureProvider, nameof(azureProvider));
      
      this.azureProvider = azureProvider;
      this.Enabled = enabled;
    }

    public bool Enabled { get; set; }

    public void AddOrUpdate(string key, T value)
    {
      if (!this.Enabled)
        return;
      
      this.AddOrUpdate(new FileInfo(key), value);
    }

    public T GetValue(string filePath, Func<string, T> populateFunction)
    {
      T cacheValue = this.GetValue(filePath, true);
      if (cacheValue != null)
        return cacheValue;

      if (!this.azureProvider.FileExists(filePath))
        return default (T);

      string name = AzureUtils.FilePathToName(filePath);
      lock (AzureUtils.GetBlobLock(name))
      {
        T calculatedValue = populateFunction(name);
        this.AddOrUpdate(name, calculatedValue);
        return calculatedValue;
      }
    }

    public T GetValue(string filePath, bool validate = true)
    {
      if (!this.Enabled)
        return default (T);
      
      if (!this._cache.TryGetValue(filePath, out AzureBlobCacheEntry<T> fsCacheEntry))
        return default (T);
      
      if (!validate || (DateTime.Now - fsCacheEntry.Added).TotalMilliseconds < 1000.0)
        return fsCacheEntry.Entry;
      
      var fileInfo = new FileInfo(filePath);
      if (!fileInfo.Exists || fileInfo.LastWriteTime != fsCacheEntry.LastModified)
        return default (T);
      
      return fsCacheEntry.Entry;
    }

    public bool Remove(string key)
    {
      AzureBlobCacheEntry<T> azureBlobCacheEntry;
      return this._cache.TryRemove(key, out azureBlobCacheEntry);
    }

    public void Clear()
    {
      this._cache.Clear();
    }

    private void AddOrUpdate(FileInfo file, T value)
    {
      if (!this.Enabled || !file.Exists)
        return;
      
      var fsCacheEntry = new AzureBlobCacheEntry<T>()
      {
        Added = DateTime.Now,
        LastModified = file.LastWriteTime,
        Entry = value
      };
      
      this._cache[file.FullName] = fsCacheEntry;
    }

    private class AzureBlobCacheEntry<TEntry>
    {
      public TEntry Entry { get; set; }

      public DateTime Added { get; set; }

      public DateTime LastModified { get; set; }
    }
  }
}