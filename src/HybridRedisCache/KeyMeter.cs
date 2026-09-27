using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace HybridRedisCache;

public class KeyMeter(ILogger<KeyMeter> logger, HybridCachingOptions cacheOptions)
{
    /// <summary>
    /// The meter this library publishes on. The host must register it —
    /// <c>AddMeter(KeyMeter.MeterName)</c> — or nothing below is ever collected.
    /// </summary>
    public const string MeterName = "HybridRedisCache";

    /// <summary>
    /// Name of the counter instrument that records cache reads per layer and result.
    /// </summary>
    public const string LookupsMetricName = "hybrid_cache_lookups";

    internal const string LocalLayer = "local";
    internal const string RedisLayer = "redis";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Lookups = Meter.CreateCounter<long>(
        LookupsMetricName,
        description: "Cache reads by layer (local, redis) and result (hit, miss). A local miss falls through to redis.");

    // An instrument stays alive as long as its meter does, and the meter here is static.
    // Creating one per KeyMeter instance would publish a duplicate instrument per cache
    // instance and never release it, so instruments are shared by name instead.
    private static readonly ConcurrentDictionary<string, Lazy<Histogram<long>>> DataSizeHistograms = new();

    private readonly Histogram<long> _heavyDataUsageMetric =
        DataSizeHistograms.GetOrAdd(cacheOptions.DataSizeHistogramMetricName,
            static name => new Lazy<Histogram<long>>(() => CreateDataSizeHistogram(name),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    // Tagging the cache keeps the numbers attributable when a process hosts more than
    // one HybridCache. The cardinality is the number of cache instances, not of keys.
    private readonly KeyValuePair<string, object> _cacheTag =
        new("cache", cacheOptions.InstancesSharedName);

    private static Histogram<long> CreateDataSizeHistogram(string name) =>
        Meter.CreateHistogram<long>(
            name,
            unit: "By",
            description: "Histogram of data sizes written to Redis cache",
            advice: new InstrumentAdvice<long>
            {
                // bytes range
                HistogramBucketBoundaries =
                [
                    512, 1024, 4096, 8192, 16_384,
                    32_768, 49_152, 65_536, 98_304,
                    131_072, 262_144, 524_288, 786_432,
                    1_048_576, 2_097_152, 4_194_304, 8_388_608
                ]
            });

    public void RecordLookup(string layer, bool hit)
    {
        if (cacheOptions.EnableMeterData)
            Lookups.Add(1,
                _cacheTag,
                new("layer", layer),
                new("result", hit ? "hit" : "miss"));
    }

    public void RecordHeavyDataUsage(string key, long dataSize)
    {
        if (cacheOptions.EnableMeterData)
            _heavyDataUsageMetric.Record(dataSize, _cacheTag);

        // Check if data exceeds threshold, record metric
        if (dataSize >= cacheOptions.WarningHeavyDataThresholdBytes)
        {
            // Log warning for Splunk
            logger?.LogWarning(
                "Heavy data detected in Redis cache. Key: {Key}, Size: {DataSize} bytes, Threshold: {Threshold} bytes",
                key, dataSize, cacheOptions.WarningHeavyDataThresholdBytes);
        }
    }
}
