using System;
using System.IO;

namespace Rainbow.Storage.AzureBlob.Tests.Helpers
{
    public static class ConfigHelpers
    {
        public static string GetConnectionString()
        {
            string envValue = Environment.GetEnvironmentVariable("AZURE_BLOB_CONNECTING_STRING");
            return !string.IsNullOrEmpty(envValue) ? envValue : File.ReadAllText("../../connectionString.txt");
        }

        public static string GetContainerName() => "testing";
    }
}