# Proposal

> **Status: R&D only.** Nothing here is scheduled for implementation. The goal is to decide, with a
> small spike, whether client-side caching should replace or sit next to key-space notifications.

## Why

Today every instance keeps its local cache correct through **key-space notifications**. This has three
known problems:

1. It needs `CONFIG SET notify-keyspace-events`, which **Azure Cache for Redis and AWS ElastiCache block**.
   There the local cache only expires on its own TTL and can serve stale data.
2. Every instance gets an event for **every key write** in the database, even for keys it never cached.
3. An instance also gets the event for **its own write**, so we need `SelfWriteNotificationWindow`, which can
   hide a real write from another instance that lands inside the window.

Redis 6+ has **client-side caching** (`CLIENT TRACKING`). The server remembers which keys a client read
and sends an invalidation message only for those keys. `NOLOOP` skips the client's own writes. It needs no
`CONFIG SET`.

## What Changes

- Research how to enable `CLIENT TRACKING` through StackExchange.Redis (RESP3 push messages, or RESP2
  `REDIRECT` to the subscriber connection and the `__redis__:invalidate` channel).
- If the spike works: add an **opt-in** option (for example `InvalidationMode = KeySpace | ClientTracking`)
  that uses client tracking for local-cache invalidation. The default stays `KeySpace`.
- No public API changes other than the new option. Nothing is removed. Not breaking.

## Capabilities

### New Capabilities

- `client-side-invalidation`: Keeping each instance's local cache correct with Redis client tracking
  instead of key-space notifications.

### Modified Capabilities
<!-- none: the key-space mode keeps its current behaviour and stays the default -->

## Impact

- Code: `HybridCache.cs` (connection setup, bus subscription, reconnect handling), `HybridCachingOptions`.
- Server: Redis 6.0+ for this mode. Managed services must allow `CLIENT TRACKING` (to be verified).
- Tests: needs real Redis (container suite). Garnet support for `CLIENT TRACKING` is unknown.
- Dependency: StackExchange.Redis 3.x. Its support for tracking push messages is the main unknown.
