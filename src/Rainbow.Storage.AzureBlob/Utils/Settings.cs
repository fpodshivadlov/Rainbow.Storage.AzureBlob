using System;

namespace Rainbow.Storage.AzureBlob.Utils
{
    public static class Settings
    {
        // ToDo: move to settings if it is useful
        public static readonly int DegreeOfParallelism = Math.Min(4, Environment.ProcessorCount);

        public const int LazyAzureItemThreshold = 10240;
    }
}