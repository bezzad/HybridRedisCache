namespace HybridRedisCache;

public interface IHybridCache
{
    bool Exists(string cacheKey);
    Task<bool> ExistsAsync(string cacheKey);
    void Set<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);
    Task SetAsync<T>(string key, T value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);
    void SetAll<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);
    Task SetAllAsync<T>(IDictionary<string, T> value, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);
    T Get<T>(string key);
    T Get<T>(string cacheKey, Func<T> dataRetriever, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);
    Task<T> GetAsync<T>(string key);
    Task<T> GetAsync<T>(string cacheKey, Func<Task<T>> dataRetriever, TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true);
    void Remove(params string[] keys);
    Task RemoveAsync(params string[] keys);
    TimeSpan GetExpiration(string cacheKey);
    Task<TimeSpan> GetExpirationAsync(string cacheKey);
    void ClearAll();
    Task ClearAllAsync();
}