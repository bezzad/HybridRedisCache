namespace HybridRedisCache;

public interface IHybridCache
{
    bool Exists(string cacheKey);
    Task<bool> ExistsAsync(string cacheKey);
    void Set<T>(string key, T value, TimeSpan? expiration = null, bool fireAndForget = true);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, bool fireAndForget = true);
    void SetAll<T>(IDictionary<string, T> value, TimeSpan? expiration = null, bool fireAndForget = true);
    Task SetAllAsync<T>(IDictionary<string, T> value, TimeSpan? expiration = null, bool fireAndForget = true);
    T Get<T>(string key);
    Task<T> GetAsync<T>(string key);
    void Remove(params string[] keys);
    Task RemoveAsync(params string[] keys);
}