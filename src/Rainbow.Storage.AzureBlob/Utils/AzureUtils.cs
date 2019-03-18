namespace Rainbow.Storage.AzureBlob.Utils
{
    public static class AzureUtils
    {
        private static readonly object BlobsLock = new object();

        public static object GetBlobLock(string blobName)
        {
            // ToDo: decide if the separate sync required
            return AzureUtils.BlobsLock;
        }
        
        public static string DirectoryPathToPrefix(string path)
        {
            return AzureUtils.Sanitize($"{path.TrimEnd('/')}/");
        }
        
        public static string FilePathToName(string path)
        {
            return AzureUtils.Sanitize(path.TrimEnd('/'));
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrEmpty(value)
                ? null
                : value.Replace('\\', '/').TrimStart('/');
        }
    }
}