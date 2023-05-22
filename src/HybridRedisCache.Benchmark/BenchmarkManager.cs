using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.Caching.Memory;
using RedisCache.Benchmark;
using StackExchange.Redis;
using System.Text.Json;

namespace HybridRedisCache.Benchmark;

//[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest, BenchmarkDotNet.Order.MethodOrderPolicy.Alphabetical)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[RankColumn]
[CategoriesColumn]
public class BenchmarkManager
{
    ConnectionMultiplexer _redisConnection;
    IMemoryCache _memCache;
    IRedisCacheService _redisCache;
    EasyHybridCache _easyHybridCache;
    HybridCache _hybridCache;

    const int redisPort = 6379;
    const string redisIP = "127.0.0.1"; // "172.23.44.11"   "127.0.0.1" 
    const string KeyPrefix = "test_";
    const int ExpireDurationSecond = 3600;
    static SampleModel[] _data;
    static Lazy<SampleModel> _singleModel = new Lazy<SampleModel>(() => _data[0], true);
    static Lazy<SampleModel> _singleWorseModel = new Lazy<SampleModel>(() => _data[1], true);
    static string GenerateUniqueKey => KeyPrefix + Guid.NewGuid().ToString("N");

    //[Params(1, 10, 100)]
    public int RepeatCount { get; set; } = 1;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Write your initialization code here
        _redisConnection = ConnectionMultiplexer.Connect($"{redisIP}:{redisPort}");
        _redisCache = new RedisCacheService(_redisConnection);
        _memCache = new MemoryCache(new MemoryCacheOptions());
        _easyHybridCache = new EasyHybridCache(redisIP, redisPort);
        _data ??= Enumerable.Range(0, 10000).Select(_ => SampleModel.Factory()).ToArray();
        _hybridCache = new HybridCache(new HybridCachingOptions()
        {
            InstancesSharedName = nameof(BenchmarkManager),
            DefaultDistributedExpirationTime = TimeSpan.FromMinutes(200),
            DefaultLocalExpirationTime = TimeSpan.FromMinutes(200),
            RedisConnectString = $"{redisIP}:{redisPort},allowAdmin=true,keepAlive=500",
            ThrowIfDistributedCacheError = false,
            ConnectRetry = 0
        });
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _memCache.Dispose();
        _redisCache.Clear();
        _redisConnection.Dispose();
        _hybridCache.Dispose();
    }

    [BenchmarkCategory("Write"), Benchmark]
    public void Add_InMemory()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            _memCache.Set(KeyPrefix + GenerateUniqueKey, JsonSerializer.Serialize(_data[i]), DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));
    }

    [BenchmarkCategory("Write"), Benchmark]
    public async Task Add_InMemory_Async()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            await _memCache.GetOrCreateAsync(KeyPrefix + GenerateUniqueKey, _ => Task.FromResult(JsonSerializer.Serialize(_data[i])));
    }

    [BenchmarkCategory("Write"), Benchmark]
    public void Add_Redis()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            _redisCache.AddOrUpdate(KeyPrefix + GenerateUniqueKey, _data[i], DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));
    }

    [BenchmarkCategory("Write"), Benchmark]
    public async Task Add_Redis_Async()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            await _redisCache.AddOrUpdateAsync(KeyPrefix + GenerateUniqueKey, _data[i], DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));
    }

    [BenchmarkCategory("Write"), Benchmark]
    public void Add_Redis_With_FireAndForget()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            _redisCache.AddOrUpdate(KeyPrefix + GenerateUniqueKey, _data[i], DateTimeOffset.Now.AddSeconds(ExpireDurationSecond), true);
    }

    [BenchmarkCategory("Write"), Benchmark]
    public async Task Add_Redis_With_FireAndForget_Async()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            await _redisCache.AddOrUpdateAsync(KeyPrefix + GenerateUniqueKey, _data[i], DateTimeOffset.Now.AddSeconds(ExpireDurationSecond), true);
    }

    [BenchmarkCategory("Write"), Benchmark]
    public void Add_EasyCache_Hybrid()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            _easyHybridCache.Set(KeyPrefix + GenerateUniqueKey, _data[i], TimeSpan.FromSeconds(ExpireDurationSecond));
    }

    [BenchmarkCategory("Write"), Benchmark]
    public async Task Add_EasyCache_Hybrid_Async()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            await _easyHybridCache.SetAsync(KeyPrefix + GenerateUniqueKey, _data[i], TimeSpan.FromSeconds(ExpireDurationSecond));
    }

    [BenchmarkCategory("Write"), Benchmark]
    public void Add_HybridRedisCache()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            _hybridCache.Set(KeyPrefix + GenerateUniqueKey, _data[i], TimeSpan.FromSeconds(ExpireDurationSecond), TimeSpan.FromSeconds(ExpireDurationSecond), fireAndForget: true);
    }

    [BenchmarkCategory("Write"), Benchmark]
    public async Task Add_HybridRedisCache_Async()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            await _hybridCache.SetAsync(KeyPrefix + GenerateUniqueKey, _data[i], TimeSpan.FromSeconds(ExpireDurationSecond), TimeSpan.FromSeconds(ExpireDurationSecond), fireAndForget: true);
    }

    [BenchmarkCategory("Read"), Benchmark]
    public void Get_InMemory()
    {
        var key = GenerateUniqueKey;

        // write single cache
        _memCache.Set(key, _singleModel.Value, DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));

        // read cache
        for (var i = 0; i < RepeatCount; i++)
            if (_memCache.TryGetValue(key, out string value))
                ThrowIfIsNotMatch(JsonSerializer.Deserialize<SampleModel>(value), _singleModel.Value);
    }

    [BenchmarkCategory("Read"), Benchmark]
    public async Task Get_InMemory_Async()
    {
        var key = GenerateUniqueKey;

        // write single cache
        _memCache.Set(key, JsonSerializer.Serialize(_singleModel.Value), DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));

        // read cache
        for (var i = 0; i < RepeatCount; i++)
        {
            // don't generate correct data when couldn't find, because its already wrote!
            var value = await _memCache.GetOrCreateAsync(key, _ => Task.FromResult(JsonSerializer.Serialize(_singleWorseModel.Value)));
            ThrowIfIsNotMatch(JsonSerializer.Deserialize<SampleModel>(value), _singleModel.Value);
        }
    }

    [BenchmarkCategory("Read"), Benchmark]
    public void Get_Redis()
    {
        var key = GenerateUniqueKey;

        // write single cache
        _redisCache.AddOrUpdate(key, _singleModel.Value, DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));

        // read cache
        for (var i = 0; i < RepeatCount; i++)
            if (_redisCache.TryGetValue(key, out SampleModel value))
                ThrowIfIsNotMatch(value, _singleModel.Value);
    }

    [BenchmarkCategory("Read"), Benchmark]
    public async Task Get_Redis_Async()
    {
        var key = GenerateUniqueKey;

        // write single cache
        await _redisCache.AddOrUpdateAsync(key, _singleModel.Value, DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));

        // read cache
        for (var i = 0; i < RepeatCount; i++)
        {
            // don't generate correct data when couldn't find, because its already wrote!
            var value = await _redisCache.GetAsync(key, () => Task.FromResult(_singleWorseModel.Value), ExpireDurationSecond);
            ThrowIfIsNotMatch(value, _singleModel.Value);
        }
    }

    [BenchmarkCategory("Read"), Benchmark]
    public void Get_EasyCache_Hybrid()
    {
        var key = GenerateUniqueKey;

        // write single cache
        _easyHybridCache.Set(key, _singleModel.Value, TimeSpan.FromSeconds(ExpireDurationSecond));

        // read cache
        for (var i = 0; i < RepeatCount; i++)
        {
            // don't generate correct data when couldn't find, because its already wrote!
            var value = _easyHybridCache.Get<SampleModel>(key);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
        }
    }

    [BenchmarkCategory("Read"), Benchmark]
    public async Task Get_EasyCache_Hybrid_Async()
    {
        var key = GenerateUniqueKey;

        // write single cache
        await _easyHybridCache.SetAsync(key, _singleModel.Value, TimeSpan.FromSeconds(ExpireDurationSecond));

        // read cache
        for (var i = 0; i < RepeatCount; i++)
        {
            // don't generate correct data when couldn't find, because its already wrote!
            var value = await _easyHybridCache.GetAsync<SampleModel>(key);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
        }
    }

    [BenchmarkCategory("Read"), Benchmark]
    public void Get_HybridRedisCache()
    {
        var key = GenerateUniqueKey;

        // write single cache
        _hybridCache.Set(key, _singleModel.Value, TimeSpan.FromSeconds(ExpireDurationSecond), fireAndForget: false);

        // read cache
        for (var i = 0; i < RepeatCount; i++)
        {
            // don't generate correct data when couldn't find, because its already wrote!
            var value = _hybridCache.Get<SampleModel>(key);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
        }
    }

    [BenchmarkCategory("Read"), Benchmark]
    public async Task Get_HybridRedisCache_Async()
    {
        var key = GenerateUniqueKey;

        // write single cache
        await _hybridCache.SetAsync(key, _singleModel.Value, TimeSpan.FromSeconds(ExpireDurationSecond), fireAndForget: false);

        // read cache
        for (var i = 0; i < RepeatCount; i++)
        {
            // don't generate correct data when couldn't find, because its already wrote!
            var value = await _hybridCache.GetAsync<SampleModel>(key);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
        }
    }

    private void ThrowIfIsNotMatch(SampleModel a, SampleModel b)
    {
        if (a?.Id != b?.Id)
            throw new ArrayTypeMismatchException($"value.Id({a?.Id} not equal with _data[i].Id({b?.Id}");
    }
}
