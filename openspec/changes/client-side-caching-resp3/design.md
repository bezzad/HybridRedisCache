# Design

## Context

See proposal.md - Why. Today `HybridCache` subscribes to `__keyspace@*__:<prefix>*` and removes a local
entry on `set`, `del`, `expired` and `rename_*` events. Self-writes are filtered with `_recentlySetKeys`
and `SelfWriteNotificationWindow`. StackExchange.Redis (SE.Redis) 3.x keeps one interactive connection and
one subscriber connection per endpoint. It can speak RESP3 (`protocol=resp3`), where pub/sub shares the
interactive connection.

### How Redis client tracking works (short)

| Mode | Command | What the server sends |
| --- | --- | --- |
| Default | `CLIENT TRACKING ON [NOLOOP]` | An `invalidate` message for keys **this connection read** (GET) |
| Broadcast | `CLIENT TRACKING ON BCAST PREFIX <p> [NOLOOP]` | An `invalidate` message for **every** write under prefix `<p>` |
| RESP2 | add `REDIRECT <client-id>` | Messages go to another connection, subscribed to `__redis__:invalidate` |
| RESP3 | (no redirect) | A `>invalidate` push message on the same connection |

`NOLOOP` = do not send invalidations caused by this connection's own writes.

## Goals / Non-Goals

**Goals:**

- Find out, with a spike, whether SE.Redis 3.x can receive tracking invalidations in a reliable way.
- Pick one mode (default or BCAST, RESP2 or RESP3) and document why.
- Keep key-space mode as the default; the new mode is opt-in.

**Non-Goals:**

- Removing key-space mode.
- Tracking for hash fields or other data types (tracking works per key, which is enough).
- Cluster-wide `REDIRECT` across shards in the first version.

## Decisions

### D1. Use RESP2 + `REDIRECT` to the subscriber connection (first choice)

- The subscriber connection subscribes to `__redis__:invalidate`, like any channel. SE.Redis already
  handles this well, including resubscribe on reconnect.
- The interactive connection runs `CLIENT TRACKING ON REDIRECT <subscriber-id> ...`.
- **Alternative: RESP3 push.** Simpler on the wire, but SE.Redis does not (as far as we know) expose
  `invalidate` push frames to user code. The spike must check this. If it does expose them, RESP3 is
  better (no redirect, no client-id lookup).
- **Hard part:** getting the client id of SE.Redis's subscriber connection. Spike options:
  (a) `CLIENT LIST` and match on a unique `CLIENT SETNAME`/lib-name per connection;
  (b) run `CLIENT ID` through the subscriber connection if SE.Redis allows it.

### D2. Use BCAST with the instance prefix

- Default mode only tracks keys read by **that connection**. SE.Redis multiplexes all commands of the
  process on it, so it works, but the server memory grows with the number of tracked keys.
- BCAST with `PREFIX <InstancesSharedName>` needs no server memory per key and also matches how the
  current key-space subscription is scoped.
- Cost: messages for keys we never cached. That is the same as today, so not worse.
- The spike measures both; BCAST is the default choice for simplicity.

### D3. Use `NOLOOP` and remove the time window in this mode

`NOLOOP` makes the server skip our own writes, so `SelfWriteNotificationWindow` is not needed in this mode.
This fixes the known "another write inside the window is ignored" problem.

### D4. Reconnect = clear local cache + re-enable tracking

Tracking state belongs to one connection. After any reconnect of the interactive or subscriber connection,
run `CLIENT TRACKING` again (with the new subscriber id) and clear the local cache
(`FlushLocalCacheOnBusReconnection` logic already exists).

## Risks / Trade-offs

- [SE.Redis may not support this cleanly] → The spike is the first task; stop the change if it fails.
- [Subscriber client id changes on reconnect] → Re-run D4 on every `ConnectionRestored` event.
- [Managed services] → Verify `CLIENT TRACKING` on Azure Cache for Redis and ElastiCache before promising it.
- [Cluster] → Tracking is per node. Each primary needs its own `CLIENT TRACKING`. Keep cluster out of v1 or
  loop over all primaries.
- [Garnet in tests] → Garnet may not support tracking; tests for this mode go to the container suite.
- [Race: read then invalidate] → A value read just before an invalidation can be put into the local cache
  after the invalidation. Same risk as today; mitigate by removing the key on invalidation *and* by the
  local TTL.

## Migration Plan

Opt-in option, default unchanged, so no migration. Rollback = turn the option off.

## Open Questions

- Exact name and shape of the option (`InvalidationMode` enum vs. `bool UseClientTracking`).
- Whether to expose a metric for invalidation messages received.
