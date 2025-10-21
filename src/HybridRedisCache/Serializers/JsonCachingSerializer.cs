using System.Text;
using Newtonsoft.Json;

namespace HybridRedisCache.Serializers;

public class JsonCachingSerializer(JsonSerializerSettings options) : ICachingSerializer
{
    public byte[] Serialize<T>(T value)
    {
        if (value == null)
            return null;

        var json = JsonConvert.SerializeObject(value, options);
        return Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(byte[] bytes)
    {
        if (bytes?.Length > 0)
        {
            var json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json, options);
        }

        return default;
    }
}
