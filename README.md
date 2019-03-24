# Rainbow.Storage.AzureBlob

[![Build status](https://ci.appveyor.com/api/projects/status/4d3det65ney432i5?svg=true)](https://ci.appveyor.com/project/FyodorPodshivadlov/rainbow-storage-azureblob)
[![Tests status](https://img.shields.io/appveyor/tests/FyodorPodshivadlov/rainbow-storage-azureblob.svg)](https://ci.appveyor.com/project/FyodorPodshivadlov/rainbow-storage-azureblob)
[![NuGet](https://img.shields.io/nuget/vpre/Rainbow.Storage.AzureBlob.svg)](https://www.nuget.org/packages/Rainbow.Storage.AzureBlob)

An Azure Blob Storage data store extension library for Rainbow, Sitecore serialization library.

The current version is working only to sync changes from Azure - writing to Azure is not implemented.

#### Unicorn configuration example

The config patch file example.
```xml
<configuration name="Sitecore.Project.Demo.Media">
 
  <targetDataStore
    cloudRootPath="/media"
    containerName="serialization"
    connectionString="<AZURE_BLOB_STORAGE_CONNECTION_STRING>"
    type="Rainbow.Storage.AzureBlob.SerializationBlobStorageDataStore, Rainbow.Storage.AzureBlob"
    useDataCache="false"
    useBlobListCache="false"
    useBigFilesLazyLoad="false"
    singleInstance="true"
    patch:instead="targetDataStore[@type='Rainbow.Storage.SerializationFileSystemDataStore, Rainbow']"
  />

</configuration>
```

#### Parameters

- `useDataCache="false"` - default Rainbow configuration.

- `useBlobListCache="true"` initialize the full list of file to memory and doesn't update the file list.
It makes sense to use if you need to sync many items.

- `useBigFilesLazyLoad="true"` do lazy loading of items to reduce usage of memory.
It makes sense to use in case if:
1) you upload big size items.
2) you have a lot of dependencies (Unicorn uses the mechanism of retrying keeping the data in memory).
