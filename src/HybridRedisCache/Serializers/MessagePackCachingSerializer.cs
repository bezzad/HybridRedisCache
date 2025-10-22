using MessagePack;
using MessagePack.Resolvers;

namespace HybridRedisCache.Serializers;

public class MessagePackCachingSerializer : ICachingSerializer
{
    public MessagePackCachingSerializer()
    {
        MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard; //.WithResolver(ContractlessStandardResolver.);
    }

    public byte[] Serialize<T>(T value)
    {
        if (value == null)
            return null;

        return MessagePackSerializer.Serialize(value);
    }

    public T Deserialize<T>(byte[] bytes)
    {
        if (bytes?.Length > 0)
            return MessagePackSerializer.Deserialize<T>(bytes);

        return default;
    }
}
