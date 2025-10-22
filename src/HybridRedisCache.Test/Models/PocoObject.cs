using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using MemoryPack;
using MessagePack;

namespace HybridRedisCache.Test.Models;

[MemoryPackable]
[MessagePackObject]
[KnownType(typeof(Armor))]
[KnownType(typeof(Weapon))]
[Union(0, typeof(Armor))]
[Union(1, typeof(Weapon))]
public partial class Item : IEquatable<Item>
{
    [Key(0)] public int Id { get; set; } = 0;
    [Key(1)] public string Name { get; set; } = "";
    [Key(2)] public string Icon { get; set; } = "";
    [Key(3)] public int MaxValue { get; set; } = 1;
    [Key(4)] public bool IsStackable { get; set; } = true;
    [Key(5)] public DateTime Timestamp { get; set; }

    public bool Equals(Item other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Id == other.Id && Name == other.Name && Icon == other.Icon && MaxValue == other.MaxValue && IsStackable == other.IsStackable && Timestamp.Equals(other.Timestamp);
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((Item)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name, Icon, MaxValue, IsStackable, Timestamp);
    }
}

[MemoryPackable]
[MessagePackObject]
public partial class Weapon : Item, IEquatable<Weapon>
{
    [Key(6)] public float Reload { get; set; } = 1;
    [Key(7)] public float Recoil { get; set; } = 2;
    [Key(8)] public float Weight { get; set; } = 4;
    [Key(9)] public float Melee { get; set; } = 3;
    [Key(10)] public float Damage { get; set; } = 4;

    public bool Equals(Weapon other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return base.Equals(other) && Reload.Equals(other.Reload) && Recoil.Equals(other.Recoil) && Weight.Equals(other.Weight) && Melee.Equals(other.Melee) && Damage.Equals(other.Damage);
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((Weapon)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Reload, Recoil, Weight, Melee, Damage);
    }
}

[MemoryPackable]
[MessagePackObject]

public partial class Armor : Item
{
    [Key(6)] public float Strength { get; set; } = 14;
}

// Container class for a polymorphic collection
[MessagePackObject]
[MemoryPackable]
public partial class ItemCache
{
    [Key(0)]
    public List<Item> Items { get; set; } = new();
}