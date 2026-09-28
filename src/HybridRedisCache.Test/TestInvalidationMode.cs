using System;

namespace HybridRedisCache.Test;

/// <summary>
/// Lets the whole suite run in either invalidation mode:
/// <c>HYBRIDCACHE_TEST_INVALIDATION_MODE=ClientTracking dotnet test ...</c>. Defaults to key-space.
/// </summary>
internal static class TestInvalidationMode
{
    public static InvalidationMode Current { get; } =
        Enum.TryParse<InvalidationMode>(Environment.GetEnvironmentVariable("HYBRIDCACHE_TEST_INVALIDATION_MODE"),
            ignoreCase: true, out var mode)
            ? mode
            : InvalidationMode.KeySpace;
}
