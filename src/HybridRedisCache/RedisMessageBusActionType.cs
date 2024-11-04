namespace HybridRedisCache;

internal enum RedisMessageBusActionType
{
    InvalidateCacheKeys, // Clear specific keys from the local cache
    NotifyLockReleased, // Signal that a lock has been released, allowing waiters to reattempt acquiring the lock
    ClearAllLocalCache // Clear the entire local cache
}