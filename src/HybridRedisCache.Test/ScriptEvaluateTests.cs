using System;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

public class ScriptEvaluateTests(ITestOutputHelper testOutputHelper) : BaseCacheTest(testOutputHelper)
{
    [Fact]
    public async Task ScriptEvaluateAsync_WithStringScript_ShouldExecuteSuccessfully()
    {
        // Arrange
        var key = UniqueKey;
        var value = "test-value";
        await Cache.SetAsync(key, value, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        var luaScript = "return redis.call('GET', KEYS[1])";
        var keys = new[] { key };

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript, keys);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithStringScript_SetAndGetValue_ShouldReturnCorrectValue()
    {
        // Arrange
        var key = UniqueKey;
        var value = "script-test-value";
        var luaScript = "redis.call('SET', KEYS[1], ARGV[1]); return redis.call('GET', KEYS[1])";
        var keys = new[] { key };
        var values = new[] { value };

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript, keys, values);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
        // The script returns the value directly from Redis
        Assert.Equal(value, (string)result);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithStringScript_MultipleKeys_ShouldExecuteSuccessfully()
    {
        // Arrange
        var key1 = UniqueKey;
        var key2 = UniqueKey;
        var value1 = "value1";
        var value2 = "value2";

        await Cache.SetAsync(key1, value1, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        await Cache.SetAsync(key2, value2, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        var luaScript = @"
            local val1 = redis.call('GET', KEYS[1])
            local val2 = redis.call('GET', KEYS[2])
            return {val1, val2}
        ";
        var keys = new[] { key1, key2 };

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript, keys);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithStringScript_ReturnNumber_ShouldReturnCorrectValue()
    {
        // Arrange
        var luaScript = "return 42";

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
        Assert.Equal(42, (int)result);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithStringScript_IncrementValue_ShouldWork()
    {
        // Arrange
        var key = UniqueKey;
        var luaScript = @"
            redis.call('SET', KEYS[1], 0)
            redis.call('INCR', KEYS[1])
            redis.call('INCR', KEYS[1])
            return redis.call('GET', KEYS[1])
        ";
        var keys = new[] { key };

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript, keys);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, (int)result);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithStringScript_NullKeysAndValues_ShouldExecuteSuccessfully()
    {
        // Arrange
        var luaScript = "return 'Hello from Lua'";

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
        Assert.Equal("Hello from Lua", (string)result);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithStringScript_WithFlags_ShouldExecuteSuccessfully()
    {
        // Arrange
        var key = UniqueKey;
        var value = "flagged-value";
        await Cache.SetAsync(key, value, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        var luaScript = "return redis.call('GET', KEYS[1])";
        var keys = new[] { key };

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript, keys, flags: Flags.PreferMaster);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithLuaScript_ShouldExecuteSuccessfully()
    {
        // Arrange
        var key = UniqueKey;
        var value = "lua-script-value";
        await Cache.SetAsync(key, value, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        var luaScript = LuaScript.Prepare("return redis.call('GET', @key)");
        var parameters = new { key = $"{Options.InstancesSharedName}:{key}" };

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithLuaScript_SetValue_ShouldWork()
    {
        // Arrange
        var key = UniqueKey;
        var value = "prepared-script-value";
        var luaScript = LuaScript.Prepare("redis.call('SET', @key, @value); return redis.call('GET', @key)");
        var parameters = new { key = $"{Options.InstancesSharedName}:{key}", value };

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
        var retrievedValue = await Cache.GetAsync<string>(key);
        Assert.Equal(value, retrievedValue);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithLuaScript_MultipleParameters_ShouldExecuteSuccessfully()
    {
        // Arrange
        var key1 = UniqueKey;
        var key2 = UniqueKey;
        var value1 = "value-one";
        var value2 = "value-two";

        var luaScript = LuaScript.Prepare(@"
            redis.call('SET', @key1, @value1)
            redis.call('SET', @key2, @value2)
            return redis.call('GET', @key1) .. ':' .. redis.call('GET', @key2)
        ");
        var parameters = new
        {
            key1,
            key2,
            value1,
            value2
        };

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
        Assert.Equal($"{value1}:{value2}", (string)result);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithLuaScript_NullParameters_ShouldExecuteSuccessfully()
    {
        // Arrange
        var luaScript = LuaScript.Prepare("return 'No parameters needed'");

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
        Assert.Equal("No parameters needed", (string)result);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithLuaScript_WithFlags_ShouldExecuteSuccessfully()
    {
        // Arrange
        var key = UniqueKey;
        var value = "flagged-lua-value";
        await Cache.SetAsync(key, value, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        var luaScript = LuaScript.Prepare("return redis.call('GET', @key)");
        var parameters = new { key = $"{Options.InstancesSharedName}:{key}" };

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript, parameters, Flags.PreferMaster);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithLoadedLuaScript_ShouldExecuteSuccessfully()
    {
        // Arrange
        var key = UniqueKey;
        var value = "loaded-script-value";
        await Cache.SetAsync(key, value, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        var luaScript = LuaScript.Prepare("return redis.call('GET', @key)");
        var loadedScript = await luaScript.LoadAsync(Cache.RedisDb.Multiplexer.GetServer(Cache.RedisDb.Multiplexer.GetEndPoints().First()));
        var parameters = new { key = $"{Options.InstancesSharedName}:{key}" };

        // Act
        var result = await Cache.ScriptEvaluateAsync(loadedScript, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithLoadedLuaScript_SetValue_ShouldWork()
    {
        // Arrange
        var key = UniqueKey;
        var value = "loaded-set-value";
        var luaScript = LuaScript.Prepare("redis.call('SET', @key, @value); return redis.call('GET', @key)");
        var loadedScript = luaScript.Load(Cache.RedisDb.Multiplexer.GetServer(Cache.RedisDb.Multiplexer.GetEndPoints().First()));
        var parameters = new { key = $"{Options.InstancesSharedName}:{key}", value };

        // Act
        var result = await Cache.ScriptEvaluateAsync(loadedScript, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
        var retrievedValue = await Cache.GetAsync<string>(key);
        Assert.Equal(value, retrievedValue);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithLoadedLuaScript_ComplexOperation_ShouldExecuteSuccessfully()
    {
        // Arrange
        var key = UniqueKey;
        var luaScript = LuaScript.Prepare(@"
            redis.call('SET', @key, 10)
            for i = 1, 5 do
                redis.call('INCR', @key)
            end
            return redis.call('GET', @key)
        ");
        var loadedScript = await luaScript.LoadAsync(Cache.RedisDb.Multiplexer.GetServer(Cache.RedisDb.Multiplexer.GetEndPoints().First()));
        var parameters = new { key };

        // Act
        var result = await Cache.ScriptEvaluateAsync(loadedScript, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(15, (int)result);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithLoadedLuaScript_NullParameters_ShouldExecuteSuccessfully()
    {
        // Arrange
        var luaScript = LuaScript.Prepare("return 123456");
        var loadedScript = luaScript.Load(Cache.RedisDb.Multiplexer.GetServer(Cache.RedisDb.Multiplexer.GetEndPoints().First()));

        // Act
        var result = await Cache.ScriptEvaluateAsync(loadedScript);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(123456, (int)result);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithLoadedLuaScript_WithFlags_ShouldExecuteSuccessfully()
    {
        // Arrange
        var key = UniqueKey;
        var value = "flagged-loaded-value";
        await Cache.SetAsync(key, value, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        var luaScript = LuaScript.Prepare("return redis.call('GET', @key)");
        var loadedScript = luaScript.Load(Cache.RedisDb.Multiplexer.GetServer(Cache.RedisDb.Multiplexer.GetEndPoints().First()));
        var parameters = new { key = $"{Options.InstancesSharedName}:{key}" };

        // Act
        var result = await Cache.ScriptEvaluateAsync(loadedScript, parameters, Flags.PreferMaster);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithStringScript_DeleteKey_ShouldWork()
    {
        // Arrange
        var key = UniqueKey;
        var value = "to-be-deleted";
        await Cache.SetAsync(key, value, localCacheEnable: false, redisExpiry: TimeSpan.FromMinutes(1));

        var luaScript = "return redis.call('DEL', KEYS[1])";
        var keys = new[] { key };

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript, keys);
        var retrievedValue = await Cache.GetAsync<string>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, (int)result);
        Assert.Null(retrievedValue);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithStringScript_ConditionalLogic_ShouldExecuteCorrectly()
    {
        // Arrange
        var key = UniqueKey;
        var luaScript = @"
            local value = redis.call('GET', KEYS[1])
            if value then
                return 'exists'
            else
                return 'not-exists'
            end
        ";
        var keys = new[] { key };

        // Act - first call without setting the key
        var result1 = await Cache.ScriptEvaluateAsync(luaScript, keys);

        // Set the key
        await Cache.SetAsync(key, "some-value", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        // Act - second call after setting the key
        var result2 = await Cache.ScriptEvaluateAsync(luaScript, keys);

        // Assert
        Assert.Equal("not-exists", (string)result1);
        Assert.Equal("exists", (string)result2);
    }

    [Fact]
    public async Task ScriptEvaluateAsync_WithLuaScript_ReturnArray_ShouldWork()
    {
        // Arrange
        var key1 = UniqueKey;
        var key2 = UniqueKey;
        var key3 = UniqueKey;

        await Cache.SetAsync(key1, "val1", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        await Cache.SetAsync(key2, "val2", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        await Cache.SetAsync(key3, "val3", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        var luaScript = LuaScript.Prepare(@"
            return {
                redis.call('GET', @key1),
                redis.call('GET', @key2),
                redis.call('GET', @key3)
            }
        ");
        var parameters = new
        {
            key1,
            key2,
            key3
        };

        // Act
        var result = await Cache.ScriptEvaluateAsync(luaScript, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNull);
        var array = (RedisResult[])result;
        Assert.NotNull(array);
        Assert.Equal(3, array.Length);
    }
}
