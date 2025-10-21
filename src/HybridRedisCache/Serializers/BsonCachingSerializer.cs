using JsonSerializer = System.Text.Json.JsonSerializer;

namespace HybridRedisCache.Serializers;

public class BsonCachingSerializer(JsonSerializerOptions options) : ICachingSerializer
{
    public byte[] Serialize<T>(T value)
    {
        if (value == null)
            return null;

        return JsonSerializer.SerializeToUtf8Bytes(value, options);
    }

    public T Deserialize<T>(byte[] bytes)
    {
        if (bytes?.Length > 0)
            return JsonSerializer.Deserialize<T>(bytes, options);

        return default;
    }
}
