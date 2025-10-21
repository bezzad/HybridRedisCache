using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HybridRedisCache.Serializers;
using Xunit;

namespace HybridRedisCache.Test;

public class ObjectHelperTest
{
    private readonly ComplexObject _value1;
    private readonly ComplexPocoObject _value2;

    public ObjectHelperTest()
    {
        _value1 = new ComplexObject
        {
            Name = "John",
            Age = 30,
            Address = new Address
            {
                Street = "123 Main St",
                City = "Any town",
                State = "CA",
                Zip = "12345"
            },
            PhoneNumbers = ["555-1234", "555-5678"]
        };

        _value2 = new ComplexPocoObject
        {
            Name = "John",
            Age = 30,
            Address = new Location
            {
                Lat = 3.3,
                Lan = 4.4,
                Street = "123 Main St",
                City = "Any town",
                State = "CA",
                Zip = "12345"
            },
            PhoneNumbers = ["555-1234", "555-5678"],
            Parent = _value1
        };
    }

    [Theory]
    [InlineData(SerializerType.Bson)]
    [InlineData(SerializerType.MemoryPack)]
    [InlineData(SerializerType.MessagePack)]
    public void DeserializePolymorphicClasses(SerializerType serializerType)
    {
        // Act
        var option = new HybridCachingOptions { SerializerType = serializerType };
        var serializer = option.GetDefaultSerializer();
        var objBytes = serializer.Serialize(_value2);
        var result = serializer.Deserialize<IComplexObject>(objBytes);
        var realTypeResult = result as ComplexPocoObject;

        // Assert
        // verify that the retrieved object is equal to the original object
        Assert.NotNull(objBytes);
        Assert.NotNull(result);
        Assert.NotNull(realTypeResult);
        Assert.IsType<ComplexPocoObject>(result);
        Assert.IsType<ComplexObject>(realTypeResult.Parent);
        Assert.IsType<Address>(realTypeResult.Parent.Address);
        Assert.IsType<Location>(realTypeResult.Address);
        Assert.Equal(_value2.Name, result.Name);
        Assert.Equal(_value2.PhoneNumbers.First(), result.PhoneNumbers.First());
        Assert.Equal(_value2.Parent.Address.City, realTypeResult.Parent.Address.City);
    }

    [Theory]
    [InlineData(SerializerType.Bson)]
    [InlineData(SerializerType.MemoryPack)]
    [InlineData(SerializerType.MessagePack)]
    public void DeserializePolymorphicInCollections(SerializerType serializerType)
    {
        // Arrange 
        var option = new HybridCachingOptions { SerializerType = serializerType };
        var serializer = option.GetDefaultSerializer();
        ICollection<string> collection = new Collection<string>()
        {
            "test_collection_0",
            "test_collection_1",
            "test_collection_2",
            "test_collection_3",
            "test_collection_4"
        };

        // Act
        var objBytes = serializer.Serialize(collection);
        var result = serializer.Deserialize<Collection<string>>(objBytes);

        // Assert
        // verify that the retrieved object is equal to the original object
        Assert.NotNull(objBytes);
        Assert.NotNull(result);
        Assert.NotNull(result);
        Assert.IsType<Collection<string>>(result);
        for (var i = 0; i < collection.Count; i++)
        {
            Assert.Equal("test_collection_" + i, result[i]);
        }
    }

    [Theory]
    [InlineData(SerializerType.Bson)]
    [InlineData(SerializerType.MemoryPack)]
    [InlineData(SerializerType.MessagePack)]
    public void DeserializePolymorphicList(SerializerType serializerType)
    {
        // Arrange 
        var option = new HybridCachingOptions { SerializerType = serializerType };
        var serializer = option.GetDefaultSerializer();
        IList<string> collection = new List<string>
        {
            "test_list_0",
            "test_list_1",
            "test_list_2",
            "test_list_3",
            "test_list_4"
        };

        // Act
        var objBytes = serializer.Serialize(collection);
        var result = serializer.Deserialize<IList<string>>(objBytes);

        // Assert
        // verify that the retrieved object is equal to the original object
        Assert.NotNull(objBytes);
        Assert.NotNull(result);
        Assert.IsType<List<string>>(result);
        for (var i = 0; i < collection.Count; i++)
        {
            Assert.Equal("test_list_" + i, result[i]);
        }
    }

    [Theory]
    [InlineData(SerializerType.Bson)]
    [InlineData(SerializerType.MemoryPack)]
    [InlineData(SerializerType.MessagePack)]
    public void PrimitivesTypeSerializationTest(SerializerType serializerType)
    {
        // Arrange
        var doubleNum = 123456789.0123456789;
        var floatNum = 123456.012345f;
        var intNum = 1234;
        short shortNum = 1234;
        var character = 'A';
        var text = "This is a sample text";

        // Act and Assert
        PrimitivesTypeTest(doubleNum, serializerType);
        PrimitivesTypeTest(floatNum, serializerType);
        PrimitivesTypeTest(intNum, serializerType);
        PrimitivesTypeTest(shortNum, serializerType);
        PrimitivesTypeTest(character, serializerType);
        PrimitivesTypeTest(text, serializerType);
    }

    private void PrimitivesTypeTest<T>(T value, SerializerType serializerType)
    {
        // Arrange
        var option = new HybridCachingOptions { SerializerType = serializerType };
        var serializer = option.GetDefaultSerializer();
        var bytes = serializer.Serialize(value);

        // Act
        var result = serializer.Deserialize<T>(bytes);

        // Assert
        // verify that the retrieved object is equal to the original object
        Assert.NotNull(result);
        Assert.IsType<T>(result);
        Assert.Equal(value, result);
    }

    [Fact]
    public void ToTimeSpanTest()
    {
        // Arrange
        DateTime? date = DateTime.UtcNow.AddDays(16).AddHours(8).AddMinutes(40).AddSeconds(20);

        // Act
        var time = date.ToTimeSpan();

        // Assert
        // verify that the retrieved object is equal to the original object
        Assert.Equal(16, time?.Days);
        Assert.Equal(8, time?.Hours);
        Assert.Equal(40, time?.Minutes);
    }

    [Fact]
    public void MessagePackSerializerTest()
    {
        // arrange
        var option = new HybridCachingOptions { SerializerType = SerializerType.MessagePack };
        var serializer = option.GetDefaultSerializer();
        var dt1 = DateTime.Parse("2019-11-07 10:30:30");
        var value = new Weapon()
        {
            Id = 10,
            Damage = 10,
            Icon = "null",
            IsStackable = true,
            MaxValue = 100,
            Melee = 10,
            Name = "Test",
            Recoil = 1.1f,
            Reload = 30,
            Timestamp = dt1,
            Weight = 100
        };

        // action
        var bytes = serializer.Serialize(value);
        var res1 = serializer.Deserialize<Weapon>(bytes);

        // assert
        Assert.NotEmpty(bytes);
        Assert.Equal(dt1, res1.Timestamp);
        Assert.Equal(value, res1);
    }
}
