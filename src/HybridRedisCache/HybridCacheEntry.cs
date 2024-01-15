namespace HybridRedisCache
{
    public class HybridCacheEntry
    {
        public TimeSpan? LocalExpiry { get; set; }
        public TimeSpan? RedisExpiry { get; set; }
        public bool FireAndForget { get; set; }
        public bool LocalCacheEnable { get; set; }
        public bool RedisCacheEnable { get; set; }

        public HybridCacheEntry(
            TimeSpan? localExpiry = null,
            TimeSpan? redisExpiry = null,
            bool fireAndForget = true,
            bool localCacheEnable = true,
            bool redisCacheEnable = true)
        {

            LocalExpiry = localExpiry;
            RedisExpiry = redisExpiry;
            FireAndForget = fireAndForget;
            LocalCacheEnable = localCacheEnable;
            RedisCacheEnable = redisCacheEnable;
        }

        public void SetRedisExpiryUtcTime(string time24H)
        {
            RedisExpiry = time24H.GetNextUtcDateTime().GetNonZeroDurationFromNow();
        }
    }
}
