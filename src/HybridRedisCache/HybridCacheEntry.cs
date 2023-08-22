namespace HybridRedisCache
{
    public record HybridCacheEntry
    {
        public TimeSpan? LocalExpiry { get; }
        public TimeSpan? RedisExpiry { get; }
        public bool FireAndForget { get; }
        public bool LocalCacheEnable { get; }
        public bool RedisCacheEnable { get; }

        public HybridCacheEntry(TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true, bool localCacheEnable = true, bool redisCacheEnable = true) =>
            (LocalExpiry, RedisExpiry, FireAndForget, LocalCacheEnable, RedisCacheEnable) = (localExpiry, redisExpiry, fireAndForget, localCacheEnable, redisCacheEnable);
    }
}
