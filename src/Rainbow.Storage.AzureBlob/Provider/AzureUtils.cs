namespace Rainbow.Storage.AzureBlob.Provider
{
    public class AzureUtils
    {
        private static object blobsLock = new object();

        public static object GetBlobLock(string blobName)
        {
            // ToDo: decide if the separate sync required
            return AzureUtils.blobsLock;
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