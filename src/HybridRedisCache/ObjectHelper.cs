using HybridRedisCache.Serializers;

[assembly: InternalsVisibleTo("HybridRedisCache.Test")]

namespace HybridRedisCache;

internal static class ObjectHelper
{
    public static TimeSpan? ToTimeSpan(this DateTime? time)
    {
        TimeSpan? duration = null;

        if (time.HasValue)
        {
            duration = time.Value.Subtract(DateTime.UtcNow);
        }

        if (duration <= TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return duration;
    }

    /// <summary>
    /// Reports whether the server answers a PING, retrying up to <paramref name="retryCount"/> times.
    /// </summary>
    /// <remarks>
    /// A reply means the server is reachable, however slowly: latency is not a verdict here. Judging
    /// it (this used to call a reply over two seconds a failure) turned a loaded-but-healthy server
    /// into a full reconnect, which is the more expensive outcome.
    /// </remarks>
    public static async Task<bool> PingAsync(this IDatabase redisDb, int retryCount)
    {
        if (redisDb is null)
            return false;

        // Always try at least once: a caller configuring ConnectRetry = 0 was reported as "down"
        // without a single PING being sent.
        var attempts = Math.Max(1, retryCount);

        for (var i = 0; i < attempts; i++)
        {
            try
            {
                await redisDb.PingAsync().ConfigureAwait(false);
                return true;
            }
            catch
            {
                // Swallow and retry, but do not sleep after the final attempt.
                if (i < attempts - 1)
                    await Task.Delay(500).ConfigureAwait(false);
            }
        }

        return false;
    }
    
    /// <summary>
    /// Observes <paramref name="token"/> while awaiting a Redis operation.
    /// </summary>
    /// <remarks>
    /// StackExchange.Redis does not accept a <see cref="CancellationToken"/> on its command APIs.
    /// Cancelling therefore stops <i>this caller</i> from awaiting the result; the command itself has
    /// already been handed to the multiplexer and may still be executed by the server. Use it to bound
    /// how long a caller waits, not to guarantee the write never happens.
    /// </remarks>
    public static Task<T> Cancelable<T>(this Task<T> task, CancellationToken token)
    {
        return token.CanBeCanceled ? task.WaitAsync(token) : task;
    }

    /// <inheritdoc cref="Cancelable{T}(Task{T}, CancellationToken)"/>
    public static Task Cancelable(this Task task, CancellationToken token)
    {
        return token.CanBeCanceled ? task.WaitAsync(token) : task;
    }

    public static ICachingSerializer GetDefaultSerializer(this HybridCachingOptions options)
    {
        return options.SerializerType switch
        {
            SerializerType.MemoryPack => new MemoryPackCachingSerializer(),
            SerializerType.MessagePack => new MessagePackCachingSerializer(),
            SerializerType.Bson => new BsonCachingSerializer(options.BsonSerializerSettings),
            _ => throw new InvalidOperationException("No valid serializer configured in HybridCachingOptions.")
        };
    }
}