using System;
using System.Collections.Concurrent;
using System.Threading;
using Rainbow.Storage.AzureBlob.Manager;
using Rainbow.Storage.AzureBlob.Utils;
using Sitecore.Collections;
using Sitecore.Diagnostics;

namespace Rainbow.Storage.AzureBlob
{
   public sealed class AzureBlobCache<T> where T : class
  {
    private readonly ConcurrentDictionary<string, AzureBlobCacheEntry<T>> _cache = 
      new ConcurrentDictionary<string, AzureBlobCacheEntry<T>>(StringComparer.OrdinalIgnoreCase);
    private ConcurrentSet<string> processingFileNames = new ConcurrentSet<string>();

    private readonly AzureManager azureManager;
    
    public AzureBlobCache(AzureManager azureManager, bool enabled)
    {
      Assert.ArgumentNotNull(azureManager, nameof(azureManager));
      
      this.azureManager = azureManager;
      this.Enabled = enabled;
    }

    public bool Enabled { get; set; }

    public void AddOrUpdate(string filePath, T value)
    {
      if (!this.Enabled)
        return;
      
      this.AddOrUpdateCache(filePath, value);
    }

    public T GetValue(string filePath, Func<string, T> populateFunction)
    {
      T cacheValue = this.GetValue(filePath, true);
      if (cacheValue != null)
        return cacheValue;

      if (!this.azureManager.FileExists(filePath))
        return default (T);

      string name = AzureUtils.FilePathToName(filePath);

      var mutex = new Mutex(false, $"blob-{name}");
      try
      {
        mutex.WaitOne();
        T cacheValueAgain = this.GetValue(filePath, true);
        if (cacheValueAgain != null)
          return cacheValueAgain;
        
        T calculatedValue = populateFunction(name);
        this.AddOrUpdateCache(name, calculatedValue);
        return calculatedValue;
      }
      finally
      {
        mutex.ReleaseMutex();
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
      
      // ToDo: check if exists or modified
      //if (!fileInfo.Exists || fileInfo.LastWriteTime != fsCacheEntry.LastModified)
      //  return default (T);
      
      return fsCacheEntry.Entry;
    }

    public bool Remove(string key)
    {
      return this._cache.TryRemove(key, out AzureBlobCacheEntry<T> _);
    }

    public void Clear()
    {
      this._cache.Clear();
    }

    private void AddOrUpdateCache(string filePath, T value)
    {
      if (!this.Enabled || !this.azureManager.FileExists(filePath))
        return;
      
      var fsCacheEntry = new AzureBlobCacheEntry<T>
      {
        Added = DateTime.Now,
        // ToDo: implement modified time
        ////LastModified = file.LastWriteTime,
        Entry = value
      };
      
      this._cache[filePath] = fsCacheEntry;
    }

    private class AzureBlobCacheEntry<TEntry>
    {
      public TEntry Entry { get; set; }

      public DateTime Added { get; set; }

      ////public DateTime LastModified { get; set; }
    }
  }
}