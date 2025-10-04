using Prometheus;

namespace HybridRedisCache;

public class KeyMeter(ILogger<KeyMeter> logger, HybridCachingOptions cacheOptions)
{
    private readonly Histogram _heavyDataUsageMetric = Metrics.CreateHistogram(
        cacheOptions.DataSizeHistogramMetricName,
        "Histogram of data sizes written to Redis cache",
        new HistogramConfiguration
        {
            // bytes range
            Buckets =
            [
                512, 1024, 4096, 8192, 16_384,
                32_768, 49_152, 65_536, 98_304,
                131_072, 262_144, 524_288, 786_432,
                1_048_576, 2_097_152, 4_194_304, 8_388_608
            ]
        });

    public void RecordHeavyDataUsage(string key, long dataSize)
    {
        // Record Prometheus metric with key label
        if (cacheOptions.EnableMeterData)
            _heavyDataUsageMetric.Observe(dataSize);

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
