namespace HybridRedisCache.Serializers;

public static class CachingJsonSerializerOptions
{
    public static JsonSerializerSettings Default => new()
    {
        // There is no polymorphic deserialization (equivalent to Newtonsoft.Json's TypeNameHandling)
        // support built-in to System.Text.Json.
        // TypeNameHandling.All will write and use type names for objects and collections.
        TypeNameHandling = TypeNameHandling.All,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None,
        MaxDepth = 64
    };
}
