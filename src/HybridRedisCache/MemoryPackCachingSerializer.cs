using MemoryPack;

namespace HybridRedisCache;

public class MemoryPackCachingSerializer : ICachingSerializer
{
    private readonly MemoryPackSerializerOptions _memoryPackSerializerOptions = MemoryPackSerializerOptions.Utf8;

    public byte[] Serialize<T>(T value)
    {
        if (value == null)
            return null;
        
        return MemoryPackSerializer.Serialize(value, _memoryPackSerializerOptions);
    }

    public T Deserialize<T>(byte[] bytes)
    {
        if (bytes?.Length > 0)
            return MemoryPackSerializer.Deserialize<T>(bytes, _memoryPackSerializerOptions);

        return default;
    }
}
