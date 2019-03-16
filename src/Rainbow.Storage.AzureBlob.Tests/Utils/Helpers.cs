using System.IO;

namespace Rainbow.Storage.AzureBlob.Tests.Utils
{
    public static class Helpers
    {
        public static string GetConnectionString() => File.ReadAllText("connectionString.txt");
        
        public static string GetContainerName() => "testing";
    }
}