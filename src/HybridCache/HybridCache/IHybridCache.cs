public interface IHybridCache
{
    void Set<T>(string key, T value, TimeSpan? expiration = null);
    T Get<T>(string key);
    void Remove(string key);

    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task<T> GetAsync<T>(string key);
    Task RemoveAsync(string key);
}