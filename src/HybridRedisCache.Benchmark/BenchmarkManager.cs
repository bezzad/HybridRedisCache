using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.Caching.Memory;
using RedisCache.Benchmark;
using StackExchange.Redis;
using System.Text.Json;

namespace HybridRedisCache.Benchmark;

[MemoryDiagnoser]
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
    const string ReadKeyPrefix = "test_x";
    const int ExpireDurationSecond = 3600;
    static SampleModel[] _data;
    static Lazy<SampleModel> _singleModel = new Lazy<SampleModel>(() => _data[0], true);
    static Lazy<SampleModel> _singleWorseModel = new Lazy<SampleModel>(() => _data[1], true);

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
            RedisCacheConnectString = $"{redisIP}:{redisPort}",
            ThrowIfDistributedCacheError = false,
            BusRetryCount = 0
        });
    }

    [Benchmark(Baseline = true)]
    public void Add_InMemory()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            _memCache.Set(KeyPrefix + i, JsonSerializer.Serialize(_data[i]), DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));
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
    public async Task Add_InMemory_Async()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            await _memCache.GetOrCreateAsync(KeyPrefix + i, _ => Task.FromResult(JsonSerializer.Serialize(_data[i])));
    }

    [BenchmarkCategory("Write"), Benchmark]
    public void Add_Redis()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            _redisCache.AddOrUpdate(KeyPrefix + i, _data[i], DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));
    }

    [BenchmarkCategory("Write"), Benchmark]
    public async Task Add_Redis_Async()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            await _redisCache.AddOrUpdateAsync(KeyPrefix + i, _data[i], DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));
    }

    [BenchmarkCategory("Write"), Benchmark]
    public void Add_Redis_With_FireAndForget()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            _redisCache.AddOrUpdate(KeyPrefix + i, _data[i], DateTimeOffset.Now.AddSeconds(ExpireDurationSecond), true);
    }

    [BenchmarkCategory("Write"), Benchmark]
    public async Task Add_Redis_With_FireAndForget_Async()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            await _redisCache.AddOrUpdateAsync(KeyPrefix + i, _data[i], DateTimeOffset.Now.AddSeconds(ExpireDurationSecond), true);
    }

    [BenchmarkCategory("Write"), Benchmark]
    public void Add_EasyCache_Hybrid()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            _easyHybridCache.Set(KeyPrefix + i, _data[i], TimeSpan.FromSeconds(ExpireDurationSecond));
    }

    [BenchmarkCategory("Write"), Benchmark]
    public async Task Add_EasyCache_Hybrid_Async()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            await _easyHybridCache.SetAsync(KeyPrefix + i, _data[i], TimeSpan.FromSeconds(ExpireDurationSecond));
    }

    [BenchmarkCategory("Write"), Benchmark]
    public void Add_HybridRedisCache()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            _hybridCache.Set(KeyPrefix + i, _data[i], TimeSpan.FromSeconds(ExpireDurationSecond), TimeSpan.FromSeconds(ExpireDurationSecond), fireAndForget: true);
    }

    [BenchmarkCategory("Write"), Benchmark]
    public async Task Add_HybridRedisCache_Async()
    {
        // write cache
        for (var i = 0; i < RepeatCount; i++)
            await _hybridCache.SetAsync(KeyPrefix + i, _data[i], TimeSpan.FromSeconds(ExpireDurationSecond), TimeSpan.FromSeconds(ExpireDurationSecond), fireAndForget: true);
    }

    [BenchmarkCategory("Read"), Benchmark]
    public void Get_InMemory()
    {
        // write single cache
        _memCache.Set(ReadKeyPrefix, _singleModel.Value, DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));

        // read cache
        for (var i = 0; i < RepeatCount; i++)
            if (_memCache.TryGetValue(ReadKeyPrefix, out string value))
                ThrowIfIsNotMatch(JsonSerializer.Deserialize<SampleModel>(value), _singleModel.Value);
    }

    [BenchmarkCategory("Read"), Benchmark]
    public async Task Get_InMemory_Async()
    {
        // write single cache
        _memCache.Set(ReadKeyPrefix, JsonSerializer.Serialize(_singleModel.Value), DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));

        // read cache
        for (var i = 0; i < RepeatCount; i++)
        {
            // don't generate correct data when couldn't find, because its already wrote!
            var value = await _memCache.GetOrCreateAsync(ReadKeyPrefix, _ => Task.FromResult(JsonSerializer.Serialize(_singleWorseModel.Value)));
            ThrowIfIsNotMatch(JsonSerializer.Deserialize<SampleModel>(value), _singleModel.Value);
        }
    }

    [BenchmarkCategory("Read"), Benchmark]
    public void Get_Redis()
    {
        // write single cache
        _redisCache.AddOrUpdate(ReadKeyPrefix, _singleModel.Value, DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));

        // read cache
        for (var i = 0; i < RepeatCount; i++)
            if (_redisCache.TryGetValue(ReadKeyPrefix, out SampleModel value))
                ThrowIfIsNotMatch(value, _singleModel.Value);
    }

    [BenchmarkCategory("Read"), Benchmark]
    public async Task Get_Redis_Async()
    {
        // write single cache
        await _redisCache.AddOrUpdateAsync(ReadKeyPrefix, _singleModel.Value, DateTimeOffset.Now.AddSeconds(ExpireDurationSecond));

        // read cache
        for (var i = 0; i < RepeatCount; i++)
        {
            // don't generate correct data when couldn't find, because its already wrote!
            var value = await _redisCache.GetAsync(ReadKeyPrefix, () => Task.FromResult(_singleWorseModel.Value), ExpireDurationSecond);
            ThrowIfIsNotMatch(value, _singleModel.Value);
        }
    }

    [BenchmarkCategory("Read"), Benchmark]
    public void Get_EasyCache_Hybrid()
    {
        // write single cache
        _easyHybridCache.Set(ReadKeyPrefix, _singleModel.Value, TimeSpan.FromSeconds(ExpireDurationSecond));

        // read cache
        for (var i = 0; i < RepeatCount; i++)
        {
            // don't generate correct data when couldn't find, because its already wrote!
            var value = _easyHybridCache.Get<SampleModel>(ReadKeyPrefix);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
        }
    }

    [BenchmarkCategory("Read"), Benchmark]
    public async Task Get_EasyCache_Hybrid_Async()
    {
        // write single cache
        await _easyHybridCache.SetAsync(ReadKeyPrefix, _singleModel.Value, TimeSpan.FromSeconds(ExpireDurationSecond));

        // read cache
        for (var i = 0; i < RepeatCount; i++)
        {
            // don't generate correct data when couldn't find, because its already wrote!
            var value = await _easyHybridCache.GetAsync<SampleModel>(ReadKeyPrefix);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
        }
    }

    [BenchmarkCategory("Read"), Benchmark]
    public void Get_HybridRedisCache()
    {
        // write single cache
        _hybridCache.Set(ReadKeyPrefix, _singleModel.Value, TimeSpan.FromSeconds(ExpireDurationSecond), fireAndForget: true);

        // read cache
        for (var i = 0; i < RepeatCount; i++)
        {
            // don't generate correct data when couldn't find, because its already wrote!
            var value = _easyHybridCache.Get<SampleModel>(ReadKeyPrefix);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
        }
    }

    [BenchmarkCategory("Read"), Benchmark]
    public async Task Get_HybridRedisCache_Async()
    {
        // write single cache
        await _hybridCache.SetAsync(ReadKeyPrefix, _singleModel.Value, TimeSpan.FromSeconds(ExpireDurationSecond), fireAndForget: true);

        // read cache
        for (var i = 0; i < RepeatCount; i++)
        {
            // don't generate correct data when couldn't find, because its already wrote!
            var value = await _hybridCache.GetAsync<SampleModel>(ReadKeyPrefix);
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
