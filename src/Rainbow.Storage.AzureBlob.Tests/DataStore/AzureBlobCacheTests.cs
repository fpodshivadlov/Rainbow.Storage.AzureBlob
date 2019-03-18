using NSubstitute;
using Rainbow.Storage.AzureBlob.Manager;
using Xunit;

namespace Rainbow.Storage.AzureBlob.Tests.DataStore
{
    public class AzureBlobCacheTests
    {
        [Fact]
        public void GetValue()
        {
            var azureManager = Substitute.For<IAzureManager>();
            azureManager.FileExists(Arg.Any<string>()).Returns(x => x.Arg<string>() == "key");
            
            var cache = new AzureBlobCache<string>(azureManager, true);
            string result1 = cache.GetValue("key", x => "value");
            Assert.Equal("value", result1);
            
            string result2 = cache.GetValue("key", x => "should_not_be");
            Assert.Equal("value", result2);
        }
        
        [Fact]
        public void AddOrUpdate()
        {
            var azureManager = Substitute.For<IAzureManager>();
            azureManager.FileExists(Arg.Any<string>()).Returns(x => x.Arg<string>() == "key");
            
            var cache = new AzureBlobCache<string>(azureManager, true);
            cache.AddOrUpdate("key", "value");
            string result2 = cache.GetValue("key", x => "should_not_be");
            Assert.Equal("value", result2);
        }
    }
}