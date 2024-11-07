namespace HybridRedisCache;

internal enum MessageType
{
    SetKey,
    RemoveKey, // generates a del event for every deleted key.
    ExpiredKey, // events generated every time a key expiress
    ExpireKey, // events generated when a key is set to expire
    ClearLocalCache, // custom event to clear the local cache
    NewKey, // events generated when a new key is added
    EvictedKey // events generated when a key is evicted for maxmemory
}

internal static class RedisMessageBusActionType
{
    public static bool Is(this RedisValue value, MessageType type)
    {
        return value == type.GetValue();
    }

    public static string GetValue(this MessageType type)
    {
        return type switch
        {
            MessageType.SetKey => "set",
            MessageType.RemoveKey => "del",
            MessageType.ExpiredKey => "expired",
            MessageType.ExpireKey => "expire",
            MessageType.ClearLocalCache => "clearmemory",
            MessageType.NewKey => "new",
            MessageType.EvictedKey => "evicted",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}