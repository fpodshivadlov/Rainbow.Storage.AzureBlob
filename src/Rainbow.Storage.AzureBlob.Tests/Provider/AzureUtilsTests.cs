using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rainbow.Storage.AzureBlob.Provider;
using Rainbow.Storage.AzureBlob.Tests.Utils;
using Xunit;

namespace Rainbow.Storage.AzureBlob.Tests
{
    public class AzureUtilsTests
    {       
        [Theory]
        [InlineData("/media/Imported/Imported.yml")]
        [InlineData("\\media\\Imported\\Imported.yml")]
        [InlineData("media/Imported/Imported.yml/")]
        public void FilePathToName(string filePath)
        {
            string name = AzureUtils.FilePathToName(filePath);
            
            Assert.StartsWith("media", name);
            Assert.EndsWith(".yml", name);
        }
        
        [Theory]
        [InlineData("/media/Imported/By Type/")]
        [InlineData("\\media\\Imported\\By Type\\")]
        [InlineData("\\media/Imported/By Type")]
        [InlineData("media/Imported/By Type")]
        public void DirectoryPathToPrefix(string filePath)
        {
            string prefix = AzureUtils.DirectoryPathToPrefix(filePath);
            
            Assert.StartsWith("media", prefix);
            Assert.EndsWith("/", prefix);
        }
    }
}