namespace HybridRedisCache;

/// <summary>
/// How the local cache of every instance is kept in step with Redis.
/// </summary>
public enum InvalidationMode
{
    /// <summary>
    /// Key-space notifications (<c>CONFIG SET notify-keyspace-events</c>). Every instance receives an event
    /// for every write under <see cref="HybridCachingOptions.InstancesSharedName"/>; own writes are filtered
    /// with <see cref="HybridCachingOptions.SelfWriteNotificationWindow"/>.
    /// </summary>
    KeySpace = 0,

    /// <summary>
    /// Redis client tracking (<c>CLIENT TRACKING ON REDIRECT BCAST PREFIX NOLOOP</c>, Redis 6+). Needs no
    /// <c>CONFIG SET</c>, and the server skips this instance's own writes, so no time window is used.
    /// Requires <see cref="HybridCachingOptions.AllowAdmin"/>.
    /// </summary>
    ClientTracking = 1,
}
