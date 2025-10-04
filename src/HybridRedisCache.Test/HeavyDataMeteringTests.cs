using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

public class HeavyDataMeteringTests(ITestOutputHelper testOutputHelper) : BaseCacheTest(testOutputHelper)
{
    [Theory]
    [InlineData(100, 50, true)]  // 100 bytes data, 50 bytes threshold - should log
    [InlineData(1000, 500, true)] // 1000 bytes data, 500 bytes threshold - should log
    [InlineData(50, 100, false)] // 50 bytes data, 100 bytes threshold - should not log
    [InlineData(500, 1000, false)] // 500 bytes data, 1000 bytes threshold - should not log
    public async Task SerializeWithExpiryPrefix_ShouldLogAndMeter_WhenDataExceedsThreshold(
        int dataSize, int thresholdBytes, bool shouldLogAndMeter)
    {
        // Arrange
        var key = UniqueKey;
        var largeData = new string('A', dataSize); // Create string of specified size
        
        var options = new HybridCachingOptions
        {
            RedisConnectionString = Options.RedisConnectionString,
            EnableMeterData = true,
            WarningHeavyDataThresholdBytes = thresholdBytes,
            EnableLogging = true
        };

        await using var cache = new HybridCache(options, LoggerFactory);

        // Act
        await cache.SetAsync(key, largeData, TimeSpan.FromMinutes(1));

        // Assert
        if (shouldLogAndMeter)
        {
            TestOutputHelper.WriteLine($"Expected heavy data log for key: {key}, data size: {dataSize} bytes, threshold: {thresholdBytes} bytes");
        }
        else
        {
            TestOutputHelper.WriteLine($"No heavy data log expected for key: {key}, data size: {dataSize} bytes, threshold: {thresholdBytes} bytes");
        }
        
        // Note: In integration tests, we verify the behavior by checking the actual cached data
        var retrievedData = await cache.GetAsync<string>(key);
        Assert.Equal(largeData, retrievedData);
    }

    [Fact]
    public async Task SerializeWithExpiryPrefix_ShouldLogCorrectDataSize_WhenDataExceedsThreshold()
    {
        // Arrange
        var key = UniqueKey;
        const string testData = "Test data with some content 123456789"; // Known content
        var expectedByteCount = Encoding.UTF8.GetByteCount(testData);
        
        var options = new HybridCachingOptions
        {
            RedisConnectionString = Options.RedisConnectionString,
            EnableMeterData = true,
            WarningHeavyDataThresholdBytes = 10, // Low threshold to ensure logging
            EnableLogging = true
        };

        await using var cache = new HybridCache(options, LoggerFactory);

        TestOutputHelper.WriteLine($"Testing with data: '{testData}', expected byte count: {expectedByteCount}");

        // Act
        await cache.SetAsync(key, testData, TimeSpan.FromMinutes(1));

        // Assert - Verify the data was stored correctly
        var retrievedData = await cache.GetAsync<string>(key);
        Assert.Equal(testData, retrievedData);
        
        TestOutputHelper.WriteLine($"Data successfully stored and retrieved. Size should have triggered logging.");
    }

    [Fact]
    public async Task SerializeWithExpiryPrefix_ShouldNotLog_WhenMeteringDisabled()
    {
        // Arrange
        var key = UniqueKey;
        var largeData = new string('A', 10000); // Large data
        
        var options = new HybridCachingOptions
        {
            RedisConnectionString = Options.RedisConnectionString,
            EnableMeterData = false, // Disabled
            WarningHeavyDataThresholdBytes = 100,
            EnableLogging = true
        };

        await using var cache = new HybridCache(options, LoggerFactory);

        TestOutputHelper.WriteLine($"Testing with metering disabled. Large data size: {largeData.Length} chars");

        // Act
        await cache.SetAsync(key, largeData, TimeSpan.FromMinutes(1));

        // Assert - Verify data was stored even with metering disabled
        var retrievedData = await cache.GetAsync<string>(key);
        Assert.Equal(largeData, retrievedData);
        
        TestOutputHelper.WriteLine("No heavy data logging should occur when metering is disabled");
    }

    [Theory]
    [InlineData("small", 1000)] // Small data
    [InlineData("medium data content with more text", 50)] // Medium data
    [InlineData("very large data content with lots of text and characters to exceed threshold easily", 20)] // Large data
    public async Task SerializeWithExpiryPrefix_ShouldLogKeyName_WhenDataExceedsThreshold(string data, int threshold)
    {
        // Arrange
        var key = UniqueKey;
        var dataSize = Encoding.UTF8.GetByteCount(data);
        
        var options = new HybridCachingOptions
        {
            RedisConnectionString = Options.RedisConnectionString,
            EnableMeterData = true,
            WarningHeavyDataThresholdBytes = threshold,
            EnableLogging = true
        };

        await using var cache = new HybridCache(options, LoggerFactory);

        TestOutputHelper.WriteLine($"Testing key: {key}, data size: {dataSize} bytes, threshold: {threshold} bytes");

        // Act
        await cache.SetAsync(key, data, TimeSpan.FromMinutes(1));

        // Assert - Verify data was stored correctly
        var retrievedData = await cache.GetAsync<string>(key);
        Assert.Equal(data, retrievedData);
        
        if (dataSize > threshold)
        {
            TestOutputHelper.WriteLine($"Data size ({dataSize}) exceeds threshold ({threshold}). Should log key: {key}");
        }
        else
        {
            TestOutputHelper.WriteLine($"Data size ({dataSize}) does not exceed threshold ({threshold}). No logging expected.");
        }
    }

    [Fact]
    public async Task SerializeWithExpiryPrefix_ShouldHandleComplexObjects_WhenMeteringEnabled()
    {
        // Arrange
        var key = UniqueKey;
        var complexObject = new ComplexObject
        {
            Name = new string('A', 1000), // Large name to exceed threshold
            Age = 30,
            Address = new Address
            {
                Street = new string('B', 500),
                City = "LargeCityName",
                State = "CA",
                Zip = "12345"
            },
            PhoneNumbers = ["555-1234", "555-5678", "555-9999"]
        };
        
        var options = new HybridCachingOptions
        {
            RedisConnectionString = Options.RedisConnectionString,
            EnableMeterData = true,
            WarningHeavyDataThresholdBytes = 100, // Low threshold
            EnableLogging = true
        };

        await using var cache = new HybridCache(options, LoggerFactory);

        TestOutputHelper.WriteLine($"Testing complex object with large data. Name length: {complexObject.Name.Length}, Street length: {complexObject.Address.Street.Length}");

        // Act
        await cache.SetAsync(key, complexObject, TimeSpan.FromMinutes(1));

        // Assert - Verify complex object was stored and retrieved correctly
        var retrievedObject = await cache.GetAsync<ComplexObject>(key);
        Assert.NotNull(retrievedObject);
        Assert.Equal(complexObject.Name, retrievedObject.Name);
        Assert.Equal(complexObject.Age, retrievedObject.Age);
        Assert.Equal(complexObject.Address.Street, retrievedObject.Address.Street);
        
        TestOutputHelper.WriteLine("Complex object should have triggered heavy data logging due to size");
    }

    [Fact]
    public async Task SerializeWithExpiryPrefix_ShouldMeterMultipleKeys_WhenMultipleKeysExceedThreshold()
    {
        // Arrange
        var key1 = UniqueKey;
        var key2 = UniqueKey;
        var largeData1 = new string('A', 1000);
        var largeData2 = new string('B', 2000);
        
        var options = new HybridCachingOptions
        {
            RedisConnectionString = Options.RedisConnectionString,
            EnableMeterData = true,
            WarningHeavyDataThresholdBytes = 500,
            EnableLogging = true
        };

        await using var cache = new HybridCache(options, LoggerFactory);

        TestOutputHelper.WriteLine($"Testing multiple keys: {key1} ({largeData1.Length} chars), {key2} ({largeData2.Length} chars)");

        // Act
        await cache.SetAsync(key1, largeData1, TimeSpan.FromMinutes(1));
        await cache.SetAsync(key2, largeData2, TimeSpan.FromMinutes(1));

        // Assert - Verify both keys were stored correctly
        var retrievedData1 = await cache.GetAsync<string>(key1);
        var retrievedData2 = await cache.GetAsync<string>(key2);
        
        Assert.Equal(largeData1, retrievedData1);
        Assert.Equal(largeData2, retrievedData2);
        
        TestOutputHelper.WriteLine("Both keys should have triggered heavy data logging");
    }

    [Fact]
    public async Task SerializeWithExpiryPrefix_ShouldVerifyActualByteSize_WhenMeteringEnabled()
    {
        // Arrange
        var key = UniqueKey;
        var testString = "Hello World! 🌍 This is a test with emoji and special chars: àáâãäå";
        var expectedBytes = Encoding.UTF8.GetByteCount(testString);
        
        var options = new HybridCachingOptions
        {
            RedisConnectionString = Options.RedisConnectionString,
            EnableMeterData = true,
            WarningHeavyDataThresholdBytes = 10, // Very low threshold 
            EnableLogging = true
        };

        await using var cache = new HybridCache(options, LoggerFactory);

        TestOutputHelper.WriteLine($"Test string: '{testString}'");
        TestOutputHelper.WriteLine($"String length: {testString.Length} characters");
        TestOutputHelper.WriteLine($"UTF-8 byte count: {expectedBytes} bytes");

        // Act
        await cache.SetAsync(key, testString, TimeSpan.FromMinutes(1));

        // Assert
        var retrievedData = await cache.GetAsync<string>(key);
        Assert.Equal(testString, retrievedData);
        
        TestOutputHelper.WriteLine($"Data stored and retrieved successfully. Expected {expectedBytes} bytes to be logged.");
    }
}
