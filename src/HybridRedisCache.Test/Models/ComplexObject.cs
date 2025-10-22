using System.Collections.Generic;

namespace HybridRedisCache.Test.Models;

// example complex object for testing serialization
internal interface IComplexObject
{
    string Name { get; set; }
    int Age { get; set; }
    IAddress Address { get; set; }
    List<string> PhoneNumbers { get; set; }
}

internal class ComplexObject : IComplexObject
{
    public string Name { get; set; }
    public int Age { get; set; }
    public IAddress Address { get; set; }
    public List<string> PhoneNumbers { get; set; }
}

internal class ComplexPocoObject : IComplexObject
{
    public string Name { get; set; }
    public int Age { get; set; }
    public IAddress Address { get; set; }
    public List<string> PhoneNumbers { get; set; }
    public IComplexObject Parent { get; set; }
}

internal interface IAddress
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Zip { get; set; }
}

internal class Address : IAddress
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Zip { get; set; }
}

internal class Location : IAddress
{
    public double Lat { get; set; }
    public double Lan { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Zip { get; set; }
}
