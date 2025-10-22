using MemoryPack;

namespace HybridRedisCache.Test.Models;

[MemoryPackable(GenerateType.CircularReference)]
internal partial class CircularPerson
{
    [MemoryPackOrder(0)] public string Name { set; get; }
    [MemoryPackOrder(1)] public CircularPerson Self { set; get; }
}

[MemoryPackable]
internal partial record struct Person(string Name, string Lastname);

[MemoryPackable]
internal partial record class NestedPerson
{
    public string Name { set; get; }

    public string Lastname { set; get; }

    public NestedPerson Inner { set; get; }
}
