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
        public void SetAndGet_CacheEntryExists_ReturnsCachedValue()
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
            var key = "mykey";

            // Act
            var result = cache.Get<string>(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Set_CacheEntryIsRemoved_AfterExpiration()
        {
            // Arrange
            var cache = new HybridCache(RedisConnectionString, InstanceName, TimeSpan.FromSeconds(1));
            var key = "mykey";
            var value = "myvalue";

            // Act
            cache.Set(key, value);
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
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
            var cache = new HybridCache(RedisConnectionString, InstanceName, TimeSpan.FromSeconds(1));
            var key = "mykey";
            var value = "myvalue";

            // Act
            await cache.SetAsync(key, value);
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
    }
}