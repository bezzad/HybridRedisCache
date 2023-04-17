namespace HybridRedisCache
{
    internal class CacheInvalidationMessage
    {
        public string[] CacheKeys { get; set; }
        public string InstanceId { get; set; }

        public CacheInvalidationMessage(string instanceId, params string[] cacheKeys)
        {
            InstanceId = instanceId;
            CacheKeys = cacheKeys;
        }
    }
}
