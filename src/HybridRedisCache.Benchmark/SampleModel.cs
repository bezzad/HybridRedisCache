using RedisCache.Benchmark;

namespace HybridRedisCache.Benchmark;

/// <summary>
/// This class is a sample model to test caching
/// </summary>
internal class SampleModel
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public double Ratio { get; set; }
    public string[] Addresses { get; set; }
    public string State { get; set; }
    public bool HaveAccess { get; set; }

    public static SampleModel Factory()
    {
        var random = new Random(DateTime.Now.GetHashCode());

        return new SampleModel()
        {
            Id = random.NextInt64(),
            Name = random.NextString(10),
            Description = random.NextString(100, @"abcdefghijklmnopqrstuvwxyz1234567890 _@#$%^&*"),
            Ratio = random.NextDouble() * 5,
            Addresses = Enumerable.Range(0, 10).Select(_ => random.NextString(100, @"abcdefghijklmnopqrstuvwxyz1234567890 _@#$%^&*")).ToArray(),
            HaveAccess = random.Next(2) == 1,
            State = random.NextString()
        };
    }
}
