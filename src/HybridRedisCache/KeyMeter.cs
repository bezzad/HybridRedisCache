using Prometheus;

namespace HybridRedisCache;

public class KeyMeter(ILogger<KeyMeter> logger)
{
    private readonly Gauge _heavyDataUsageMetric = Metrics.CreateGauge(
        "hybrid_cache_heavy_data_bytes",
        "Number of bytes written to Redis exceeding threshold",
        new GaugeConfiguration { LabelNames = ["key"] });

    public void RecordHeavyDataUsage(string key, long dataSize, long threshold)
    {
        // Record Prometheus metric with key label
        _heavyDataUsageMetric.WithLabels(key).Set(dataSize);

        // Log warning for Splunk
        logger?.LogWarning(
            "Heavy data detected in Redis cache. Key: {Key}, Size: {DataSize} bytes, Threshold: {Threshold} bytes",
            key, dataSize, threshold);
    }
}
