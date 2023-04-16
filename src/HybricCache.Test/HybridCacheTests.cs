using StackExchange.Redis;
using System;
using System.Threading.Tasks;
using Xunit;

namespace HybricCache.Test
{
    public class HybridCacheTests
    {
        private const string RedisConnectionString = "localhost:6379";
        private const string InstanceName = "myapp";

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

    }
}