namespace HybridRedisCache;

internal class TracingActivity(string tracingActivitySourceName)
{
    public readonly ActivitySource Source = new(tracingActivitySourceName, 
        typeof(HybridCache).Assembly.GetName().Version?.ToString() ?? "");
}

internal static class ActivityExtensions
{
    internal static void SetRetrievalStrategyActivity(this Activity activity, RetrievalStrategy retrievalStrategy)
    {
        activity?.SetTag(nameof(HybridRedisCache) + ".RetrievalStrategy", retrievalStrategy.ToString("G"));
    }

    internal static void SetCacheHitActivity(this Activity activity, CacheResultType cacheResult, string cacheKey)
    {
        activity?.SetTag(nameof(HybridRedisCache) + ".CacheResult", cacheResult.ToString("G"))
            ?.SetTag(nameof(HybridRedisCache) + ".CacheKey", cacheKey);
    }
}

internal enum OperationTypes
{
    GetCache,
    SetCache,
    SetBatchCache,
    ClearLocalCache,
    KeyLookup,
    KeyLookupAsync,
    DeleteCache,
    BatchDeleteCache,
    Flush,
    Ping,
    GetExpiration,
    RemoveWithPattern,
    ReleaseLock,
    LockKey,
    ExtendLockKey,
    LockKeyObject,
    GetServerTime,
    Echo,
    DatabaseSize,
    GetSentinelInfo,
    GetServerVersion,
    SetCacheWithDataRetriever,
    KeyExpire
}

internal enum RetrievalStrategy
{
    MemoryCache,
    RedisCache,
    DataRetrieverExecution,
}

internal enum CacheResultType
{
    Hit,
    Miss
}
