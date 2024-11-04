namespace HybridRedisCache;

internal enum MessageType
{
    InvalidateCacheKey,
    ReleaseLockKey,
    ClearLocalMemory
}