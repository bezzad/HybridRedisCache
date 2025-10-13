namespace HybridRedisCache;

public partial class HybridCache
{
    public async Task SetHashAsync(string key, IDictionary<string, string> fields, TimeSpan? redisExpiry = null, Flags flags = Flags.PreferMaster)
    {
        var cacheKey = GetCacheKey(key);
        var hashSet = Array.ConvertAll(fields.ToArray(), kvp => new HashEntry(kvp.Key, kvp.Value));
        await RedisDb.HashSetAsync(cacheKey, hashSet, (CommandFlags)flags).ConfigureAwait(false);

        if (redisExpiry.HasValue)
            await RedisDb.HashFieldExpireAsync(cacheKey, fields.Keys.Select(k => (RedisValue)k).ToArray(),
                redisExpiry.Value, ExpireWhen.Always, (CommandFlags)flags).ConfigureAwait(false);
    }

    public async Task SetHashAsync(string key, string hashField, string value, Condition when = Condition.Always, Flags flags = Flags.PreferMaster)
    {
        var cacheKey = GetCacheKey(key);
        await RedisDb.HashSetAsync(cacheKey, hashField, value, (When)when, (CommandFlags)flags).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, string>> GetHashAsync(string key)
    {
        var cacheKey = GetCacheKey(key);
        var fields = await RedisDb.HashGetAllAsync(cacheKey).ConfigureAwait(false);
        return fields.ToDictionary(f => f.Name.ToString(), f => f.Value.ToString());
    }
    
    public async Task<string> GetHashAsync(string key, string hashField)
    {
        var cacheKey = GetCacheKey(key);
        return await RedisDb.HashGetAsync(cacheKey, hashField).ConfigureAwait(false);
    }
    
    public async Task<string[]> GetHashAsync(string key, string[] hashFields)
    {
        var cacheKey = GetCacheKey(key);
        var values = await RedisDb.HashGetAsync(cacheKey, hashFields.Select(k => (RedisValue)k).ToArray()).ConfigureAwait(false);
        return values.Select(v=> v.ToString()).ToArray();
    }

    public async Task<bool> HashExistAsync(string key, string hashField, Flags flags = Flags.PreferMaster)
    {
        var cacheKey = GetCacheKey(key);
        return await RedisDb.HashExistsAsync(cacheKey, hashField, (CommandFlags)flags).ConfigureAwait(false);
    }

    public async Task<bool> DeleteHashAsync(string key, string hashField, Flags flags = Flags.PreferMaster)
    {
        var cacheKey = GetCacheKey(key);
        return await RedisDb.HashDeleteAsync(cacheKey, hashField, (CommandFlags)flags).ConfigureAwait(false);
    }

    public async Task<long> DeleteHashAsync(string key, string[] hashFields, Flags flags = Flags.PreferMaster)
    {
        var cacheKey = GetCacheKey(key);
        return await RedisDb.HashDeleteAsync(cacheKey, hashFields.Select(k => (RedisValue)k).ToArray(), (CommandFlags)flags).ConfigureAwait(false);
    }
}
