using System.Diagnostics;

namespace HybridRedisCache.Utilities;

internal static class ActivityInstance
{
    internal static readonly ActivitySource Source = new(HybridCacheConstants.DefaultListenerName, 
        typeof(HybridCache).Assembly.GetName().Version?.ToString() ?? "");

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
    GetSentinelInfo
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
