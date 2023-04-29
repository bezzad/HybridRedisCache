using EasyCaching.Core;
using EasyCaching.Core.Configurations;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace RedisCache.Benchmark
{
    public class EasyHybridCache
    {
        private readonly IHybridCachingProvider _provider;

        public EasyHybridCache(string redisIp, int redisPort)
        {
            IServiceCollection services = new ServiceCollection();
            services.AddEasyCaching(option =>
            {
                option.WithJson("myjson");

                // local
                option.UseInMemory("inmemory");

                // distributed
                option.UseRedis(config =>
                {
                    config.DBConfig.Endpoints.Add(new ServerEndPoint(redisIp, redisPort));
                    config.DBConfig.Database = 5;
                    config.SerializerName = "myjson";
                    config.CacheNulls = false;
                }, "redis");

                // combine local and distributed
                option.UseHybrid(config =>
                {
                    config.TopicName = "benchmark-topic";
                    config.EnableLogging = false;

                    // specify the local cache provider name after v0.5.4
                    config.LocalCacheProviderName = "inmemory";

                    // specify the distributed cache provider name after v0.5.4
                    config.DistributedCacheProviderName = "redis";

                    config.ThrowIfDistributedCacheError = false;
                });
                // use redis bus
                //.WithRedisBus(busConf =>
                // {
                //     busConf.Endpoints.Add(new ServerEndPoint("127.0.0.1", 6380));

                //     // do not forget to set the SerializerName for the bus here !!
                //     busConf.SerializerName = "myjson";
                // });
            });

            IServiceProvider serviceProvider = services.BuildServiceProvider();
            _provider = serviceProvider.GetService<IHybridCachingProvider>();
        }

        public T Get<T>(string key)
        {
            var result = _provider.Get<T>(key);
            return result.Value;
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var result = await _provider.GetAsync<T>(key);
            return result.Value;
        }

        public void Set<T>(string key, T value, TimeSpan expiration)
        {
            _provider.Set(key, value, expiration);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiration)
        {
            await _provider.SetAsync(key, value, expiration);
        }
    }
}
