namespace HybridRedisCache;

public record RedisLockObject(IHybridCache Cache, string Key, string Token) : IDisposable, IAsyncDisposable
{
    // ReSharper disable once MemberCanBePrivate.Global
    public Task<bool> ReleaseAsync() => Cache.TryReleaseLockAsync(Key, Token);

    // ReSharper disable once MemberCanBePrivate.Global
    public bool Release() => Cache.TryReleaseLock(Key, Token);

    public void Dispose() =>  Release();

    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync().ConfigureAwait(false);
    }
}
