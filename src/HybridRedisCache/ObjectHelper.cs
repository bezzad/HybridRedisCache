using Newtonsoft.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("HybridRedisCache.Test")]
namespace HybridRedisCache
{
    internal static class ObjectHelper
    {
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings()
        {
            // There is no polymorphic deserialization (equivalent to Newtonsoft.Json's TypeNameHandling)
            // support built-in to System.Text.Json.
            TypeNameHandling = TypeNameHandling.Objects,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
        };

        public static TimeSpan ToTimeSpan(this DateTime? time)
        {
            TimeSpan duration = TimeSpan.Zero;

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

            var text = JsonConvert.SerializeObject(value, typeof(T), _jsonSettings);
            return text;
        }

        public static T Deserialize<T>(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return default;
            }

            return JsonConvert.DeserializeObject<T>(value, _jsonSettings);
        }
    }
}
