using System.Collections.Generic;

namespace HybridRedisCache.Test
{
    // example complex object for testing serialization
    internal class ComplexObject
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public Address Address { get; set; }
        public List<string> PhoneNumbers { get; set; }
    }

    // example nested object for testing serialization
    internal class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
    }
}
