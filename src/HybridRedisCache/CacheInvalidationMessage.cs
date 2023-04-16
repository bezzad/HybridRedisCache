namespace HybridRedisCache
{
    internal class CacheInvalidationMessage
    {
        public string CacheKey { get; set; }
        public string InstanceId { get; set; }

        public CacheInvalidationMessage(string cacheKey, string instanceId)
        {
            CacheKey = cacheKey;
            InstanceId = instanceId;
        }
    }
}
