using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
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

        [Fact]
        public void GetValue_Multithreads()
        {
            var azureManager = Substitute.For<IAzureManager>();
            azureManager.FileExists(Arg.Any<string>()).Returns(x => x.Arg<string>() == "key");
            
            var cache = new AzureBlobCache<string>(azureManager, true);
            var results = new ConcurrentBag<string>();

            void Action(int number) => results.Add(cache.GetValue("key", x => $"value-{number}"));

            var threads = new Thread[10];
            for (var i = 0; i < threads.Length; i++)
            {
                int number = i;
                threads[i] = new Thread(() => Action(number));
            }

            foreach(Thread thread in threads)
            {
                thread.Start();
            }

            foreach(Thread thread in threads)
            {
                thread.Join();
            }

            Assert.Equal(10, results.Count);
            Assert.Equal(1, results.Distinct().Count());
        }
    }
}