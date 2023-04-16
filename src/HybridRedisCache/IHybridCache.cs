namespace HybridRedisCache;

public interface IHybridCache
{
    void Set<T>(string key, T value, TimeSpan? expiration = null, bool fireAndForget = true);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, bool fireAndForget = true);
    T Get<T>(string key);
    Task<T> GetAsync<T>(string key);
    void Remove(string key);
    Task RemoveAsync(string key);
}