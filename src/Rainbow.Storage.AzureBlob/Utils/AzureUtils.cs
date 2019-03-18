namespace Rainbow.Storage.AzureBlob.Utils
{
    public static class AzureUtils
    {
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