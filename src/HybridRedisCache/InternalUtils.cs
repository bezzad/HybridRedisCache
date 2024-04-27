using System.Collections.Concurrent;
using System.Diagnostics;

namespace HybridRedisCache;

internal class InternalUtils
{
    internal static class Cached
    {
        internal static readonly Lazy<ActivitySource> Source = new(() =>
            new ActivitySource(HybridCacheConstants.DefaultListenerName, typeof(HybridCache).Assembly.GetName().Version?.ToString() ?? ""));

    }


    internal enum OperationTypes
    {
        GetCache,
        SetCache,
        SetBatchCache,
        ClearLocalCache,
        KeyLookup,
        DeleteCache,
        Flush
    }

    public enum RetrievalStrategy
    {
        MemoryCache,
        RedisCache,
        DataRetrieverExecution,
    }

    public enum CacheResult
    {
        Hit,
        Miss
    }
}