using MemoryPack;

namespace HybridRedisCache.Serializers;

public class MemoryPackCachingSerializer : ICachingSerializer
{
    private readonly MemoryPackSerializerOptions _options = MemoryPackSerializerOptions.Utf8;

    public byte[] Serialize<T>(T value)
    {
        if (value == null)
            return null;
        
        return MemoryPackSerializer.Serialize(value, _options);
    }

    public T Deserialize<T>(byte[] bytes)
    {
        if (bytes?.Length > 0)
            return MemoryPackSerializer.Deserialize<T>(bytes, _options);

        return default;
    }
}
