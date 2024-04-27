using System.Diagnostics;

namespace HybridRedisCache.Utilities;

internal static class ActivityInstance
{
    internal static readonly ActivitySource Source = new ActivitySource(HybridCacheConstants.DefaultListenerName, 
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
    Flush
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
