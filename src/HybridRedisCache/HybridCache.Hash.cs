using System.Collections.Immutable;

namespace HybridRedisCache;

public partial class HybridCache
{
    public async Task HashSetAsync(string key, IDictionary<string, string> fields, TimeSpan? redisExpiry = null,
        Condition when = Condition.Always, Flags flags = Flags.PreferMaster)
    {
        var cacheKey = GetCacheKey(key);
        var hashSet = Array.ConvertAll(fields.ToArray(), kvp => new HashEntry(kvp.Key, kvp.Value));
        await RedisDb.HashFieldSetAndSetExpiryAsync(cacheKey, hashSet, redisExpiry, false, (When)when, (CommandFlags)flags).ConfigureAwait(false);
    }

    public async Task<bool> HashSetAsync(string key, string hashField, string value, Condition when = Condition.Always, Flags flags = Flags.PreferMaster)
    {
        var cacheKey = GetCacheKey(key);
        return await RedisDb.HashSetAsync(cacheKey, hashField, value, (When)when, (CommandFlags)flags).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, string>> HashGetAsync(string key, Flags flags = Flags.None)
    {
        var cacheKey = GetCacheKey(key);
        var fields = await RedisDb.HashGetAllAsync(cacheKey, (CommandFlags)flags).ConfigureAwait(false);
        return fields.ToDictionary(f => f.Name.ToString(), f => f.Value.ToString());
    }

    public async Task<string> HashGetAsync(string key, string hashField, Flags flags = Flags.None)
    {
        var cacheKey = GetCacheKey(key);
        return await RedisDb.HashGetAsync(cacheKey, hashField, (CommandFlags)flags).ConfigureAwait(false);
    }

    public async Task<string[]> HashGetAsync(string key, string[] hashFields, Flags flags = Flags.None)
    {
        var cacheKey = GetCacheKey(key);
        var values = await RedisDb.HashGetAsync(cacheKey, hashFields.Select(k => (RedisValue)k).ToArray(), (CommandFlags)flags).ConfigureAwait(false);
        return values.Select(v => v.ToString()).ToArray();
    }

    public async Task<bool> HashExistsAsync(string key, string hashField, Flags flags = Flags.PreferMaster)
    {
        var cacheKey = GetCacheKey(key);
        return await RedisDb.HashExistsAsync(cacheKey, hashField, (CommandFlags)flags).ConfigureAwait(false);
    }

    public async Task<bool> HashDeleteAsync(string key, string hashField, Flags flags = Flags.PreferMaster)
    {
        var cacheKey = GetCacheKey(key);
        return await RedisDb.HashDeleteAsync(cacheKey, hashField, (CommandFlags)flags).ConfigureAwait(false);
    }

    public async Task<long> HashDeleteAsync(string key, string[] hashFields, Flags flags = Flags.PreferMaster)
    {
        var cacheKey = GetCacheKey(key);
        return await RedisDb.HashDeleteAsync(cacheKey, hashFields.Select(k => (RedisValue)k).ToArray(), (CommandFlags)flags).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<KeyValuePair<string, string>> HashScanAsync(string key, string pattern, Flags flags = Flags.None)
    {
        var cacheKey = GetCacheKey(key);
        await foreach (var entry in RedisDb.HashScanAsync(cacheKey, pattern, flags: (CommandFlags)flags))
        {
            yield return new KeyValuePair<string, string>(entry.Name.ToString(), entry.Value.ToString());
        }
    }

    public async IAsyncEnumerable<string> HashScanNoValuesAsync(string key, string pattern, Flags flags = Flags.None)
    {
        var cacheKey = GetCacheKey(key);
        await foreach (var value in RedisDb.HashScanNoValuesAsync(cacheKey, pattern, flags: (CommandFlags)flags))
        {
            yield return value.ToString();
        }
    }

    public async Task<string[]> HashValuesAsync(string key, Flags flags = Flags.None)
    {
        var cacheKey = GetCacheKey(key);
        var values = await RedisDb.HashValuesAsync(cacheKey, (CommandFlags)flags).ConfigureAwait(false);
        return values.Select(v => v.ToString()).ToArray();
    }

    public async Task<string[]> HashKeysAsync(string key, Flags flags = Flags.None)
    {
        var cacheKey = GetCacheKey(key);
        var keys = await RedisDb.HashKeysAsync(cacheKey, (CommandFlags)flags).ConfigureAwait(false);
        return keys.Select(k => k.ToString()).ToArray();
    }

    public async Task<string> HashFieldGetAndDeleteAsync(string key, string hashField, Flags flags = Flags.None)
    {
        var cacheKey = GetCacheKey(key);
        var value = await RedisDb.HashFieldGetAndDeleteAsync(cacheKey, hashField, (CommandFlags)flags).ConfigureAwait(false);
        return value.ToString();
    }

    public async Task<long> HashLengthAsync(string key, Flags flags = Flags.None)
    {
        var cacheKey = GetCacheKey(key);
        return await RedisDb.HashLengthAsync(cacheKey, (CommandFlags)flags).ConfigureAwait(false);
    }
}
