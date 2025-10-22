namespace HybridRedisCache.Serializers;

public enum SerializerType
{
    /// <summary>
    /// Zero encoding extreme performance binary serializer
    /// </summary>
    MemoryPack,
    
    /// <summary>
    /// MessagePack has APIs to allow for deserializing data without type restrictions. Per MessagePack Security Notes, these APIs should be avoided.
    /// </summary>
    MessagePack,
    
    /// <summary>
    /// Use <see cref="Newtonsoft.Json"/> to serialize objects to BSON (Binary JSON) format.
    /// In this format, data is stored in a binary representation of JSON, which is more efficient for storage and transmission compared to plain text JSON.
    /// We can keep type of objects during serialization and deserialization by including type information ($type) in the BSON data.
    /// Polymorphism is supported through this type information, allowing accurate reconstruction of derived types during deserialization.
    /// </summary>
    Bson
}
