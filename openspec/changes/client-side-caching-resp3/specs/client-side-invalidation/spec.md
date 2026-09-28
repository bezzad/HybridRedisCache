# Spec Delta

## Purpose

Keep the local memory cache of every instance consistent with Redis by using Redis client tracking,
so it works even where `CONFIG SET` is blocked.

## ADDED Requirements

### Requirement: Opt-in invalidation mode

The cache SHALL keep key-space notification invalidation as the default, and SHALL use client tracking
only when the user turns it on in the options.

#### Scenario: Default options

- **WHEN** a cache is created without choosing an invalidation mode
- **THEN** it behaves exactly as today (key-space notifications)

#### Scenario: Client tracking chosen

- **WHEN** the user chooses the client-tracking mode
- **THEN** the cache does not call `CONFIG SET notify-keyspace-events` at startup

### Requirement: Invalidate on remote change

In client-tracking mode, when another client changes, deletes, renames or expires a key that this instance
holds in its local cache, the instance SHALL remove that key from its local cache.

#### Scenario: Another instance updates a key

- **WHEN** instance A caches key `k` locally and instance B sets `k` to a new value
- **THEN** instance A's next read of `k` returns the new value

#### Scenario: A non-HybridCache client deletes a key

- **WHEN** instance A caches key `k` locally and `redis-cli` runs `DEL k`
- **THEN** instance A's next read of `k` does not return the old value

### Requirement: Own writes do not invalidate

In client-tracking mode, an instance's own write SHALL NOT remove the value it just stored in its local
cache, and SHALL NOT depend on a time window to decide that.

#### Scenario: Set then read on the same instance

- **WHEN** instance A sets `k` and reads it right away
- **THEN** the read is served from the local cache

### Requirement: Safe on connection loss

In client-tracking mode, when the connection used for tracking is lost, the instance SHALL clear its local
cache, because invalidations sent while it was disconnected are lost.

#### Scenario: Reconnect

- **WHEN** the tracking connection drops and reconnects
- **THEN** the local cache is empty after reconnect and tracking is enabled again

### Requirement: Clear fallback when not supported

If the server refuses `CLIENT TRACKING`, the cache SHALL log an error and continue in the same degraded
mode used today when `CONFIG SET` fails (local entries only expire on their TTL). Startup SHALL NOT fail.

#### Scenario: Server without tracking

- **WHEN** client-tracking mode is chosen and the server returns an error for `CLIENT TRACKING`
- **THEN** an error is logged and the cache still starts
