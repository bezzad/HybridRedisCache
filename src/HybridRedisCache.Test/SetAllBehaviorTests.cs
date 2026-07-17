using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

/// <summary>
/// Regression coverage for <see cref="HybridCache.SetAll{T}(IDictionary{string,T}, HybridCacheEntry)"/>
/// and its async counterpart, which used to write the whole dictionary into the local cache under every
/// key instead of that key's own value.
/// </summary>
public class SetAllBehaviorTests(InProcessRedisFixture fixture, ITestOutputHelper output)
    : InProcessCacheTest(fixture, output)
{
    [Fact]
    public async Task SetAllAsync_WithRedisDisabled_EachKeyReturnsItsOwnValue()
    {
        var prefix = UniqueKey;
        var data = new Dictionary<string, string>
        {
            [prefix + "a"] = "value-a",
            [prefix + "b"] = "value-b",
            [prefix + "c"] = "value-c",
        };

        // With Redis disabled the local cache is the only store, so a bad write is unrecoverable
        // rather than being masked by a Redis read.
        var result = await Cache.SetAllAsync(data, localCacheEnable: true, redisCacheEnable: false);

        Assert.True(result);
        foreach (var kvp in data)
            Assert.Equal(kvp.Value, await Cache.GetAsync<string>(kvp.Key));
    }

    [Fact]
    public void SetAll_WithRedisDisabled_EachKeyReturnsItsOwnValue()
    {
        var prefix = UniqueKey;
        var data = new Dictionary<string, string>
        {
            [prefix + "a"] = "value-a",
            [prefix + "b"] = "value-b",
        };

        var result = Cache.SetAll(data, localCacheEnable: true, redisCacheEnable: false);

        Assert.True(result);
        foreach (var kvp in data)
            Assert.Equal(kvp.Value, Cache.Get<string>(kvp.Key));
    }

    [Fact]
    public async Task SetAllAsync_WithRedisEnabled_EachKeyReturnsItsOwnValue()
    {
        var prefix = UniqueKey;
        var data = new Dictionary<string, string>
        {
            [prefix + "x"] = "value-x",
            [prefix + "y"] = "value-y",
        };

        Assert.True(await Cache.SetAllAsync(data));

        foreach (var kvp in data)
            Assert.Equal(kvp.Value, await Cache.GetAsync<string>(kvp.Key));
    }

    [Fact]
    public async Task SetAllAsync_LocalCacheHoldsValueNotTheDictionary()
    {
        var key = UniqueKey;
        await Cache.SetAllAsync(new Dictionary<string, int> { [key] = 42 },
            localCacheEnable: true, redisCacheEnable: false);

        // Reading as the element type must succeed; if the dictionary itself had been stored, the
        // typed local lookup would miss and (with Redis disabled) return default.
        var (success, value) = await Cache.TryGetValueAsync<int>(key);

        Assert.True(success);
        Assert.Equal(42, value);
    }

    [Fact]
    public async Task SetAllAsync_WithComplexType_RoundTripsPerKey()
    {
        var prefix = UniqueKey;
        var data = new Dictionary<string, ComplexModel>
        {
            [prefix + "1"] = new() { Id = 1, Name = "first" },
            [prefix + "2"] = new() { Id = 2, Name = "second" },
        };

        Assert.True(await Cache.SetAllAsync(data));

        foreach (var kvp in data)
        {
            var actual = await Cache.GetAsync<ComplexModel>(kvp.Key);
            Assert.NotNull(actual);
            Assert.Equal(kvp.Value.Id, actual.Id);
            Assert.Equal(kvp.Value.Name, actual.Name);
        }
    }

    public class ComplexModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
