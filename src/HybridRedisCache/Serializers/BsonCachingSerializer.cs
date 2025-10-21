namespace HybridRedisCache.Serializers;

public class BsonCachingSerializer(JsonSerializerOptions serializerSettings) : ICachingSerializer
{
    public byte[] Serialize<T>(T value)
    {
        if (value == null)
            return null;

        return JsonSerializer.SerializeToUtf8Bytes(value, serializerSettings);
    }

    public T Deserialize<T>(byte[] bytes)
    {
        if (bytes?.Length > 0)
            return JsonSerializer.Deserialize<T>(bytes, serializerSettings);

        return default;
    }
}
