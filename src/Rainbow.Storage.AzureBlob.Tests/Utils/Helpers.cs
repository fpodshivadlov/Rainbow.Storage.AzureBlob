using System;
using System.IO;

namespace Rainbow.Storage.AzureBlob.Tests.Utils
{
    public static class Helpers
    {
        public static string GetConnectionString()
        {
            string envValue = Environment.GetEnvironmentVariable("AZURE_BLOB_CONNECTING_STRING");
            return !string.IsNullOrEmpty(envValue) ? envValue : File.ReadAllText("../../connectionString.txt");
        }

        public static string GetContainerName() => "testing";
    }
}