namespace HybridRedisCache
{
    public record HybridCacheEntry
    {
        public TimeSpan? LocalExpiry { get; set; }
        public TimeSpan? RedisExpiry { get; set; }
        public bool FireAndForget { get; }
        public bool LocalCacheEnable { get; }
        public bool RedisCacheEnable { get; }

        public HybridCacheEntry(TimeSpan? localExpiry = null, TimeSpan? redisExpiry = null, bool fireAndForget = true, bool localCacheEnable = true, bool redisCacheEnable = true) =>
            (LocalExpiry, RedisExpiry, FireAndForget, LocalCacheEnable, RedisCacheEnable) = (localExpiry, redisExpiry, fireAndForget, localCacheEnable, redisCacheEnable);
    }
}
