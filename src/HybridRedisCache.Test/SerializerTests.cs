using System;
using System.Collections.Generic;
using HybridRedisCache.Serializers;
using MemoryPack;
using MessagePack;
using Newtonsoft.Json;
using Xunit;

namespace HybridRedisCache.Test;

/// <summary>
/// Serializer round-trip coverage. These need no Redis server at all.
/// </summary>
public class SerializerTests
{
    private static BsonCachingSerializer Bson => new(new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.All,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None,
        MaxDepth = 64
    });

    [Fact]
    public void Bson_RoundTripsComplexType()
    {
        var model = new BsonModel { Id = 7, Name = "seven", Tags = ["a", "b"] };

        var actual = Bson.Deserialize<BsonModel>(Bson.Serialize(model));

        Assert.Equal(model.Id, actual.Id);
        Assert.Equal(model.Name, actual.Name);
        Assert.Equal(model.Tags, actual.Tags);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Bson_RoundTripsInt(int value)
    {
        Assert.Equal(value, Bson.Deserialize<int>(Bson.Serialize(value)));
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("")]
    [InlineData("with \"quotes\"")]
    [InlineData("unicode: مرحبا")]
    public void Bson_RoundTripsString(string value)
    {
        Assert.Equal(value, Bson.Deserialize<string>(Bson.Serialize(value)));
    }

    [Fact]
    public void Bson_SerializeNull_ReturnsNull()
    {
        Assert.Null(Bson.Serialize<string>(null));
    }

    [Theory]
    [InlineData(null)]
    public void Bson_DeserializeNullBytes_ReturnsDefault(byte[] bytes)
    {
        Assert.Null(Bson.Deserialize<string>(bytes));
    }

    [Fact]
    public void Bson_DeserializeEmptyBytes_ReturnsDefault()
    {
        Assert.Null(Bson.Deserialize<string>([]));
        Assert.Equal(0, Bson.Deserialize<int>([]));
    }

    [Fact]
    public void Bson_RoundTripsCollection()
    {
        var value = new List<int> { 1, 2, 3 };
        Assert.Equal(value, Bson.Deserialize<List<int>>(Bson.Serialize(value)));
    }

    [Fact]
    public void Bson_RoundTripsDictionary()
    {
        var value = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        Assert.Equal(value, Bson.Deserialize<Dictionary<string, int>>(Bson.Serialize(value)));
    }

    [Fact]
    public void MessagePack_RoundTripsAnnotatedType()
    {
        var serializer = new MessagePackCachingSerializer();
        var model = new MsgPackModel { Id = 3, Name = "three" };

        var actual = serializer.Deserialize<MsgPackModel>(serializer.Serialize(model));

        Assert.Equal(model.Id, actual.Id);
        Assert.Equal(model.Name, actual.Name);
    }

    [Fact]
    public void MessagePack_SerializeNull_ReturnsNull()
    {
        Assert.Null(new MessagePackCachingSerializer().Serialize<MsgPackModel>(null));
    }

    [Fact]
    public void MemoryPack_RoundTripsAnnotatedType()
    {
        var serializer = new MemoryPackCachingSerializer();
        var model = new MemPackModel { Id = 5, Name = "five" };

        var actual = serializer.Deserialize<MemPackModel>(serializer.Serialize(model));

        Assert.Equal(model.Id, actual.Id);
        Assert.Equal(model.Name, actual.Name);
    }

    [Fact]
    public void MemoryPack_SerializeNull_ReturnsNull()
    {
        Assert.Null(new MemoryPackCachingSerializer().Serialize<MemPackModel>(null));
    }

    [Theory]
    [InlineData(SerializerType.Bson, typeof(BsonCachingSerializer))]
    [InlineData(SerializerType.MessagePack, typeof(MessagePackCachingSerializer))]
    [InlineData(SerializerType.MemoryPack, typeof(MemoryPackCachingSerializer))]
    public void GetDefaultSerializer_ReturnsConfiguredType(SerializerType type, Type expected)
    {
        var options = new HybridCachingOptions { SerializerType = type };
        Assert.IsType(expected, options.GetDefaultSerializer());
    }

    [Fact]
    public void GetDefaultSerializer_WithUnknownType_Throws()
    {
        var options = new HybridCachingOptions { SerializerType = (SerializerType)999 };
        Assert.Throws<InvalidOperationException>(() => options.GetDefaultSerializer());
    }
}

public class BsonModel
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<string> Tags { get; set; }
}

[MessagePackObject]
public class MsgPackModel
{
    [Key(0)] public int Id { get; set; }
    [Key(1)] public string Name { get; set; }
}

[MemoryPackable]
public partial class MemPackModel
{
    public int Id { get; set; }
    public string Name { get; set; }
}
