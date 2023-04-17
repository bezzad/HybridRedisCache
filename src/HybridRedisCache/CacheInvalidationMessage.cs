namespace HybridRedisCache
{
    internal class CacheInvalidationMessage
    {
        /// <summary>
        /// Gets or sets the cache keys.
        /// </summary>
        public string[] CacheKeys { get; set; }

        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        public string InstanceId { get; set; }

        public CacheInvalidationMessage(string instanceId, params string[] cacheKeys)
        {
            InstanceId = instanceId;
            CacheKeys = cacheKeys;
        }
    }
}
