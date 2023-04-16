using System;
using System.Threading.Tasks;
using Xunit;

namespace HybridRedisCache.Test
{
    public class HybridCacheTests
    {
        private const string RedisConnectionString = "localhost:6379";
        private const string InstanceName = "my-test-app";

        [Fact]
        public void ShouldCacheAndRetrieveData()
        {
            // Arrange
            var cache = new HybridCache(RedisConnectionString, InstanceName);
            var key = "mykey";
            var value = "myvalue";

            // Act
            cache.Set(key, value);
            var result = cache.Get<string>(key);

            // Assert
            Assert.Equal(value, result);
        }

        [Fact]
        public void SetAndGet_CacheEntryDoesNotExist_ReturnsNull()
        {
            // Arrange
            var cache = new HybridCache(RedisConnectionString, InstanceName);
            var key = "nonexistentkey";

            // Act
            var result = cache.Get<string>(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Set_CacheEntryIsRemoved_AfterExpiration()
        {
            // Arrange
            var cache = new HybridCache(RedisConnectionString, InstanceName);
            var key = "mykey";
            var value = "myvalue";

            // Act
            cache.Set(key, value, TimeSpan.FromMilliseconds(100));
            await Task.Delay(TimeSpan.FromSeconds(2));
            var result = cache.Get<string>(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Remove_CacheEntryIsRemoved()
        {
            // Arrange
            var cache = new HybridCache(RedisConnectionString, InstanceName);
            var key = "mykey";
            var value = "myvalue";

            // Act
            cache.Set(key, value);
            cache.Remove(key);
            var result = cache.Get<string>(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetAndGetAsync_CacheEntryExists_ReturnsCachedValue()
        {
            // Arrange
            var cache = new HybridCache(RedisConnectionString, InstanceName);
            var key = "mykey";
            var value = "myvalue";

            // Act
            await cache.SetAsync(key, value);
            var result = await cache.GetAsync<string>(key);

            // Assert
            Assert.Equal(value, result);
        }

        [Fact]
        public async Task SetAndGetAsync_CacheEntryDoesNotExist_ReturnsNull()
        {
            // Arrange
            var cache = new HybridCache(RedisConnectionString, InstanceName);
            var key = "nonexistentkey";

            // Act
            var result = await cache.GetAsync<string>(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetAsync_CacheEntryIsRemoved_AfterExpiration()
        {
            // Arrange
            var cache = new HybridCache(RedisConnectionString, InstanceName);
            var key = "mykey";
            var value = "myvalue";

            // Act
            await cache.SetAsync(key, value, TimeSpan.FromSeconds(1));
            await Task.Delay(TimeSpan.FromSeconds(2));
            var result = await cache.GetAsync<string>(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task RemoveAsync_CacheEntryIsRemoved()
        {
            // Arrange
            var cache = new HybridCache(RedisConnectionString, InstanceName);
            var key = "mykey";
            var value = "myvalue";

            // Act
            await cache.SetAsync(key, value);
            await cache.RemoveAsync(key);
            var result = await cache.GetAsync<string>(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ShouldSerializeAndDeserializeComplexObject()
        {
            // Arrange
            var cache = new HybridCache("localhost:6379", "myapp");
            var obj = new { Name = "John", Age = 30 };

            // Act
            cache.Set("mykey", obj, TimeSpan.FromMinutes(1));
            var value = cache.Get<dynamic>("mykey");

            // Assert
            Assert.Equal(obj.Name, value.Name);
            Assert.Equal(obj.Age, value.Age);
        }

        [Fact]
        public async Task TestSharedCache()
        {
            // create two instances of HybridCache that share the same Redis cache
            var instance1 = new HybridCache(RedisConnectionString, "shared-instance");
            var instance2 = new HybridCache(RedisConnectionString, "shared-instance");

            // set a value in the shared cache using instance1
            instance1.Set("mykey", "myvalue", fireAndForget: false);

            // retrieve the value from the shared cache using instance2
            var value = instance2.Get<string>("mykey");
            Assert.Equal("myvalue", value);

            // update the value in the shared cache using instance2
            instance2.Set("mykey", "newvalue", fireAndForget: false);

            // wait for cache invalidation message to be received
            await Task.Delay(1000);

            // retrieve the updated value from the shared cache using instance1
            value = instance1.Get<string>("mykey");
            Assert.Equal("newvalue", value);

            // clean up
            instance1.Dispose();
            instance2.Dispose();
        }
    }
}