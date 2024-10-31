namespace HybridRedisCache;

public class HybridCacheEntry(
    TimeSpan? localExpiry = null,
    TimeSpan? redisExpiry = null,
    bool fireAndForget = true,
    bool localCacheEnable = true,
    bool redisCacheEnable = true,
    Flags flags = Flags.PreferMaster,
    Condition when = Condition.Always,
    bool keepTtl = false)
{
    public TimeSpan? LocalExpiry { get; set; } = localExpiry;
    public TimeSpan? RedisExpiry { get; set; } = redisExpiry;
    public bool LocalCacheEnable { get; set; } = localCacheEnable;
    public bool RedisCacheEnable { get; set; } = redisCacheEnable;

    /// <summary>
    /// The caller is not interested in the result; the caller will immediately receive a default-value
    /// of the expected return type (this value is not indicative of anything at the server).
    /// </summary>
    public bool FireAndForget { get; set; } = fireAndForget;

    /// <summary>
    /// Whether to maintain the existing key's TTL (KEEPTTL flag)
    /// </summary>
    public bool KeepTtl { get; set; } = keepTtl;

    /// <summary>
    /// The flags to use for this operation
    /// </summary>
    public Flags Flags { get; set; } = fireAndForget ? flags | Flags.FireAndForget : Flags.PreferMaster;

    /// <summary>
    /// Set key to hold the string value. If key already holds a value, it is overwriiten, regardless of its type.
    /// </summary>
    /// <remarks>
    /// Which condition to set the value under (defaults to Always)
    /// </remarks>
    public Condition When { get; set; } = when;

    public void SetRedisExpiryUtcTime(string time24H)
    {
        RedisExpiry = time24H.GetNextUtcDateTime().GetNonZeroDurationFromNow();
    }
}
