using System.Collections.Generic;
using MemoryPack;

namespace HybridRedisCache.Test.Models;

// example complex object for testing serialization
[MemoryPackable]
[MemoryPackUnion(0, typeof(ComplexObject))]
[MemoryPackUnion(1, typeof(ComplexPocoObject))]
internal partial interface IComplexObject
{
    string Name { get; set; }
    int Age { get; set; }
    IAddress Address { get; set; }
    List<string> PhoneNumbers { get; set; }
}

[MemoryPackable]
internal partial class ComplexObject : IComplexObject
{
    public string Name { get; set; }
    public int Age { get; set; }
    public IAddress Address { get; set; }
    public List<string> PhoneNumbers { get; set; }
}

[MemoryPackable]
internal partial class ComplexPocoObject : IComplexObject
{
    public string Name { get; set; }
    public int Age { get; set; }
    public IAddress Address { get; set; }
    public List<string> PhoneNumbers { get; set; }
    public IComplexObject Parent { get; set; }
}

[MemoryPackable]
[MemoryPackUnion(0, typeof(Address))]
[MemoryPackUnion(1, typeof(Location))]
internal partial interface IAddress
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Zip { get; set; }
}

[MemoryPackable]
internal partial class Address : IAddress
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Zip { get; set; }
}

[MemoryPackable]
internal partial class Location : IAddress
{
    public double Lat { get; set; }
    public double Lan { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Zip { get; set; }
}
