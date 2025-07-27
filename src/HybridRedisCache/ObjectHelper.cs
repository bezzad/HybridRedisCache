[assembly: InternalsVisibleTo("HybridRedisCache.Test")]

namespace HybridRedisCache;

internal static class ObjectHelper
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        // There is no polymorphic deserialization (equivalent to Newtonsoft.Json's TypeNameHandling)
        // support built-in to System.Text.Json.
        // TypeNameHandling.All will write and use type names for objects and collections.
        TypeNameHandling = TypeNameHandling.All,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None,
    };

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

    public static string Serialize<T>(this T value)
    {
        if (value == null)
        {
            return null;
        }

        return JsonConvert.SerializeObject(value, typeof(T), JsonSettings);
    }

    public static T Deserialize<T>(this string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? default
            : JsonConvert.DeserializeObject<T>(value, JsonSettings);
    }

    public static async Task<bool> PingAsync(this IDatabase redisDb, int retryCount)
    {
        if (redisDb is null)
            return false;

        for (var i = 0; i < retryCount; i++)
        {
            try
            {
                var ping = await redisDb.PingAsync().ConfigureAwait(false);
                return ping < TimeSpan.FromSeconds(2);
            }
            catch
            {
                // Swallow and retry
                await Task.Delay(500).ConfigureAwait(false);
            }
        }

        return false;
    }
}