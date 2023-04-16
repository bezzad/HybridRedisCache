using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        [Fact]
        public void TestMultiThreadedCacheOperations()
        {
            // create a HybridCache instance
            var cache = new HybridCache(RedisConnectionString);

            // create a list of values to store in the cache
            var values = new List<string> { "foo", "bar", "baz", "qux" };

            // create multiple threads, each of which performs cache operations
            var threads = new List<Thread>();
            for (int i = 0; i < values.Count; i++)
            {
                var thread = new Thread((state) =>
                {
                    // retrieve the key and value variables from the state object
                    var tuple = (Tuple<string, string>)state;
                    var threadKey = tuple.Item1;
                    var threadValue = tuple.Item2;

                    // perform cache operations on the cache instance
                    cache.Set(threadKey, threadValue);
                    var retrievedValue = cache.Get<string>(threadKey);
                    Assert.Equal(threadValue, retrievedValue);
                    cache.Remove(threadKey);
                });

                // create a local copy of the i variable to avoid race conditions
                var localI = i;

                // start the thread and pass the key and value variables as a state object
                thread.Start(Tuple.Create($"key{i}", values[i]));

                // add the thread to the list
                threads.Add(thread);
            }

            // wait for the threads to complete
            threads.ForEach(t => t.Join());

            // clean up
            cache.Dispose();
        }

        [Fact]
        public void CacheSerializationTest()
        {
            // create a HybridRedisCache instance
            var cache = new HybridCache(RedisConnectionString);

            // create a complex object to store in the cache
            var complexObject = new ComplexObject
            {
                Name = "John",
                Age = 30,
                Address = new Address
                {
                    Street = "123 Main St",
                    City = "Anytown",
                    State = "CA",
                    Zip = "12345"
                },
                PhoneNumbers = new List<string> { "555-1234", "555-5678" }
            };

            // store the object in the cache
            cache.Set("complexObject", complexObject);

            // retrieve the object from the cache
            var retrievedObject = cache.Get<ComplexObject>("complexObject");

            // verify that the retrieved object is equal to the original object
            Assert.Equal(complexObject.Name, retrievedObject.Name);
            Assert.Equal(complexObject.Age, retrievedObject.Age);
            Assert.Equal(complexObject.Address.Street, retrievedObject.Address.Street);
            Assert.Equal(complexObject.Address.City, retrievedObject.Address.City);
            Assert.Equal(complexObject.Address.State, retrievedObject.Address.State);
            Assert.Equal(complexObject.Address.Zip, retrievedObject.Address.Zip);
            Assert.Equal(complexObject.PhoneNumbers, retrievedObject.PhoneNumbers);

            // clean up
            cache.Dispose();
        }

        [Fact]
        public void CacheConcurrencyTest()
        {
            // create a HybridRedisCache instance
            var cache = new HybridCache(RedisConnectionString);

            // create a shared key and a list of values to store in the cache
            var key = "sharedKey";
            var values = new List<string> { "foo", "bar", "baz", "qux" };

            // create multiple threads, each of which performs cache operations
            var threads = new List<Thread>();
            for (int i = 0; i < values.Count; i++)
            {
                var value = values[i];
                var thread = new Thread(() =>
                {
                    // wait for the initial value to be set by another thread
                    lock (values)
                    {
                        // perform cache operations on the cache instance
                        var currentValue = cache.Get<string>(key);
                        if (currentValue == null)
                        {
                            cache.Set(key, value, fireAndForget: false);
                        }
                        else
                        {
                            cache.Set(key, currentValue + value, fireAndForget: false);
                        }
                    }
                });

                threads.Add(thread);
            }

            // start the threads and wait for them to complete
            threads.ForEach(t => t.Start());

            // set the initial value in the cache
            cache.Set(key, values[0], fireAndForget: false);

            // waits for a brief period of time before verifying the final value in the cache
            // to ensure that all write operations have completed.
            Thread.Sleep(1000);

            // verify that the final value in the cache is correct
            var actualValue = cache.Get<string>(key);
            Assert.True(values.All(val=> actualValue.Contains(val)), $"value was:{actualValue}");

            // clean up
            cache.Dispose();
        }
    }
}