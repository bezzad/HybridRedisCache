using System.Text;

namespace HybridRedisCache.Serializers;

public class JsonCachingSerializer(JsonSerializerOptions serializerSettings) : ICachingSerializer
{
    public byte[] Serialize<T>(T value)
    {
        if (value == null)
            return null;

        var json = JsonSerializer.Serialize(value, serializerSettings);
        return Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(byte[] bytes)
    {
        if (bytes?.Length > 0)
        {
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json, serializerSettings);
        }

        return default;
    }
}
