namespace HybridRedisCache;

public enum MessageType
{
    SetCache,
    RemoveKey, // generates a del event for every deleted key.
    ExpiredKey, // events generated every time a key expires
    ExpireKey, // events generated when a key is set to expire
    ClearLocalCache, // custom event to clear the local cache
    NewKey, // events generated when a new key is added
    EvictedKey, // events generated when a key is evicted for max memory
    BuiltInMessage, // Redis inside messages
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
            MessageType.SetCache => "set",
            MessageType.RemoveKey => "del",
            MessageType.ExpiredKey => "expired",
            MessageType.ExpireKey => "expire",
            MessageType.ClearLocalCache => "clearmemory",
            MessageType.NewKey => "new",
            MessageType.EvictedKey => "evicted",
            MessageType.BuiltInMessage => "redis",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public static MessageType GetMessageType(this RedisValue value)
    {
        return (string)value switch
        {
            "set" => MessageType.SetCache,
            "del" => MessageType.RemoveKey,
            "expired" => MessageType.ExpiredKey,
            "expire" => MessageType.ExpireKey,
            "clearmemory" => MessageType.ClearLocalCache,
            "new" => MessageType.NewKey,
            "evicted" => MessageType.EvictedKey,
            "redis" => MessageType.BuiltInMessage,
            _ => MessageType.BuiltInMessage
        };
    }
}