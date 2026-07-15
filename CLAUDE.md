# HybridRedisCache

A .NET library (NuGet package `HybridRedisCache`) providing a **two-layer hybrid cache**:
an L1 in-process `MemoryCache` fronting an L2 distributed Redis cache. The headline feature
is **cross-instance coherency** — when any instance mutates a key, Redis keyspace notifications
(pub/sub) tell every other instance to evict that key from its local memory, so all app
replicas stay consistent while still getting in-memory read latency.

- **Language/TFMs**: C#, multi-targets `net8.0;net9.0;net10.0`. `ImplicitUsings` + nullable-off.
- **Current version**: 5.2.0 (see `src/HybridRedisCache/HybridRedisCache.csproj`).
- **License**: Apache-2.0. **Repo**: https://github.com/bezzad/HybridRedisCache
- **Key dependencies**: `StackExchange.Redis`, `Microsoft.Extensions.Caching.Memory`,
  `Newtonsoft.Json` (Bson), `MessagePack`, `MemoryPack`, `prometheus-net`.

## Solution layout (`src/`)

| Project | Purpose |
|---|---|
| `HybridRedisCache/` | The library itself (the only packaged/shipped project). |
| `HybridRedisCache.Test/` | xUnit tests. Uses **Testcontainers.Redis** to spin up a real `redis:latest` container per test run — no external Redis needed. |
| `HybridRedisCache.Benchmark/` | BenchmarkDotNet comparisons (Redis vs. in-memory vs. hybrid). |
| `HybridRedisCache.Sample/` | Console usage sample. |
| `HybridRedisCache.LoadTest/` | k6 (`app.js`) load test + Grafana dashboard. |

Built via `src/HybridRedisCache.sln`. CI (`.github/workflows/dotnet.yml`) restores/builds/tests
in Release on Ubuntu with .NET 10; the commented-out Redis service block is unused because tests
provision their own container via Testcontainers.

## Core architecture

`HybridCache` is a **partial class** split across three files, implementing `IHybridCache`
(sync surface, extends `IHybridCacheAsync`), plus `IDisposable`/`IAsyncDisposable`:

- `HybridCache.cs` — construction, Redis connection lifecycle, the pub/sub invalidation engine, and all private helpers.
- `HybridCache.Public.cs` — the public string/object API (Set/Get/Remove/lock/expire/scripting/server-admin), plus `RedisBusMessage`/`RedisChannelMessage` delegates.
- `HybridCache.Hash.cs` — Redis hash-field operations (`HashSetAsync`, `HashGetAsync`, `HashScanAsync`, …). Hashes are **Redis-only** (not mirrored into local memory).

### Two cache layers & key naming
- L1: `_memoryCache` (`IMemoryCache`). L2: `RedisDb` (`IDatabase`, exposed publicly).
- A second `_recentlySetKeys` memory cache (5s `_timeWindow`) tracks keys this instance just
  wrote, so it can **ignore its own** Redis "set" notifications (avoids self-invalidation).
- Every key is namespaced as `{InstancesSharedName}:{key}` via `GetCacheKey`; `GetPureCacheKey`
  strips it back. `InstancesSharedName` is what binds instances into one coherent cluster.

### Cross-instance invalidation (the heart of the design)
1. On connect, `SetRedisServersConfigs` runs `CONFIG SET notify-keyspace-events KA` (Keyspace +
   All-events) and, if `EnableRedisClientTracking`, enables `CLIENT TRACKING` with a prefix broadcast.
2. It subscribes to `__keyspace@{db}__:{InstancesSharedName}:*` → `OnBusMessage`.
3. `OnBusMessage` maps the Redis event verb (`set`/`del`/`expired`/`clearmemory`/…) via
   `MessageType`/`RedisMessageBusActionType` and evicts the affected key from `_memoryCache`
   (skipping keys in `_recentlySetKeys`). It also fires the public `OnRedisBusMessage` event and
   completes any pending distributed-lock `TaskCompletionSource` for that key.
4. `ClearLocalCache` ("clearmemory") is a **custom** app-level broadcast (not a native Redis
   event) used by `FlushLocalCaches()` to clear every instance's L1.

### Connection resilience
- `Connect` builds a `ConfigurationOptions` (linear retry, client name = `SharedName:instanceId`,
  optional `ThreadPool` socket manager) and wires `ConnectionRestored`/`ConnectionFailed`/`ErrorMessage`.
- On failure, `TryConnectAsync` (guarded by `_reconnectSemaphore`) pings with retry, then
  reconfigures if `ReconfigureOnConnectFail` — honoring `MaxReconfigureAttempts` (0 = unlimited).
  Reconfigure re-resolves DNS, useful when an endpoint's IP changed.
- `FlushLocalCacheOnBusReconnection` optionally dumps L1 on reconnect to avoid serving stale data.

### Serialization (`Serializers/`)
`ICachingSerializer` with three built-ins chosen by `SerializerType`, or inject a custom `Serializer`:
- **Bson** (default) — Newtonsoft, `TypeNameHandling.All` → supports **polymorphism** and preserves types.
- **MessagePack**, **MemoryPack** — faster/smaller binary; note MemoryPack/MessagePack type constraints.
Default is resolved lazily in the ctor via `GetDefaultSerializer()`.

### Metering (`KeyMeter.cs`)
When `EnableMeterData`, every serialized write is measured and recorded to a Prometheus
`Histogram` (name = `DataSizeHistogramMetricName`, default `hybrid_cache_data_bytes`). Writes over
`WarningHeavyDataThresholdBytes` (default 100KB) also emit a structured `LogWarning` ("heavy data").

### Tracing
`PopulateActivity` starts a `System.Diagnostics.Activity` per op when `EnableTracing`, tagged with
`OperationType`; helpers set retrieval-strategy / cache hit-miss tags (`TracingTypes.cs`).

## Public API surface (highlights)

- **Read/write**: `Set`/`SetAsync`, `SetAll`/`SetAllAsync`, `Get`/`GetAsync`, `TryGetValue(Async)`,
  `Exists(Async)`, `Remove(Async)`. Overloads take either loose params or a `HybridCacheEntry`.
- **Get-or-create**: `GetAsync(key, dataRetriever, …)` populates on miss. `FetchDataSafely`
  **de-dupes concurrent retrievals** of the same key (single-flight) via `_dataRetrieverTasks`.
  The sync `Get(..., dataRetriever, ...)` overloads are `[Obsolete]` — prefer async.
- **Per-call knobs**: `localCacheEnable`/`redisCacheEnable` (target one layer), `localExpiry` vs
  `redisExpiry` (local is clamped ≤ redis in `SetValidExpiryTimes`), `Condition` (Always/Exists/NotExists),
  `keepTtl`, and `Flags` (a mirror of SE.Redis `CommandFlags`: PreferMaster/Replica, FireAndForget, …).
- **Pattern delete**: `RemoveWithPatternOnRedisAsync` (server-side Lua SCAN+UNLINK, preferred).
  `RemoveWithPatternAsync` is `[Obsolete]` (client-side, inefficient). `KeysAsync` streams matches.
- **Distributed locking**: `TryLockKeyAsync`/`LockKeyAsync` (returns `RedisLockObject`), `TryExtendLockAsync`,
  `TryReleaseLock(Async)`. `LockKeyAsync` blocks awaiting a `TaskCompletionSource` released by the
  invalidation bus when the lock key is freed elsewhere.
- **Counters**: `ValueIncrementAsync`/`ValueDecrementAsync` (long & double).
- **Hashes**: full `Hash*` set in `HybridCache.Hash.cs`.
- **Scripting**: `ScriptEvaluateAsync` (raw Lua, `LuaScript`, `LoadedLuaScript`).
- **Server/admin**: `ClearAll(Async)` (FLUSHDB across servers), `PingAsync`, `GetServerVersion`,
  `GetServerFeatures`, `DatabaseSizeAsync`, `TimeAsync`, `EchoAsync`, `KeyExpire(Async)`,
  `GetExpiration(Async)`, and Sentinel helpers (`SentinelGet*Async`).
- **Raw pub/sub passthrough**: `Subscribe`/`Unsubscribe`/`Publish(Async)` over arbitrary channels.

### Registration
`services.AddHybridRedisCaching(options => { … })` (`HybridCacheServiceCollectionExtensions`)
registers options + `IHybridCache` as **singletons**. Or `new HybridCache(options, loggerFactory)` directly.

## Configuration (`HybridCachingOptions`, a `record`)

Notable defaults: `AbortOnConnectFail=true`, `ReconfigureOnConnectFail=false`,
`DefaultLocalExpirationTime=60min`, `DefaultDistributedExpirationTime=1day`,
`InstancesSharedName="HybridCache"`, `ConnectRetry=3`, `Sync/Async/ConnectionTimeout=5000ms`,
`SerializerType=Bson`, `EnableLogging/EnableTracing/EnableMeterData=false`. `ThrowIfDistributedCacheError`
controls whether Redis errors bubble up or are swallowed (fall back to local).

## Conventions & gotchas

- **`HybridCache` is a `partial` class** — when adding public methods, put string/object ops in
  `HybridCache.Public.cs`, hash ops in `HybridCache.Hash.cs`, private plumbing in `HybridCache.cs`.
  Keep `IHybridCache`/`IHybridCacheAsync` in sync with new public methods.
- **`Flags` is a project-local enum** cast to SE.Redis `CommandFlags` — don't leak `CommandFlags`
  onto the public API. Same for `Condition`→`When`, `ExpireCondition`→`ExpireWhen`.
- **Write path ordering**: set Redis first, then L1 — the keyspace notification races back and
  removes L1, so L1 is (re)populated only after the Redis write is acknowledged.
- **Self-notification suppression** relies on `_recentlySetKeys` (5s window). If you add new write
  paths, call `KeepRecentSetKey` so the instance doesn't evict what it just set.
- Tests are `[Collection("Sequential")]` and each derives from `BaseCacheTest`, which owns the
  Testcontainers Redis lifecycle and a lazy `Cache`. Use `PrepareDummyKeys` for pattern tests.
- `[assembly: InternalsVisibleTo("HybridRedisCache.Test")]` (in `ObjectHelper.cs`) exposes internals to tests.

## Build / test / run

```bash
dotnet restore ./src/HybridRedisCache.sln
dotnet build -c Release ./src/HybridRedisCache.sln
dotnet test  -c Release ./src/HybridRedisCache.sln   # needs Docker (Testcontainers spins up Redis)
```

Local Redis for manual runs: `docker run --name redis -p 6379:6379 -d redis:latest`.

---

# RTK (Rust Token Killer) - Token-Optimized Commands

## Golden Rule

**Always prefix commands with `rtk`**. If RTK has a dedicated filter, it uses it. If not, it passes through unchanged. This means RTK is always safe to use.

**Important**: Even in command chains with `&&`, use `rtk`:
```bash
# ❌ Wrong
git add . && git commit -m "msg" && git push

# ✅ Correct
rtk git add . && rtk git commit -m "msg" && rtk git push
```

## RTK Commands by Workflow

### Build & Compile (80-90% savings)
```bash
rtk cargo build         # Cargo build output
rtk cargo check         # Cargo check output
rtk cargo clippy        # Clippy warnings grouped by file (80%)
rtk tsc                 # TypeScript errors grouped by file/code (83%)
rtk lint                # ESLint/Biome violations grouped (84%)
rtk prettier --check    # Files needing format only (70%)
rtk next build          # Next.js build with route metrics (87%)
```

### Test (60-99% savings)
```bash
rtk cargo test          # Cargo test failures only (90%)
rtk go test             # Go test failures only (90%)
rtk jest                # Jest failures only (99.5%)
rtk vitest              # Vitest failures only (99.5%)
rtk playwright test     # Playwright failures only (94%)
rtk pytest              # Python test failures only (90%)
rtk rake test           # Ruby test failures only (90%)
rtk rspec               # RSpec test failures only (60%)
rtk test <cmd>          # Generic test wrapper - failures only
```

### Git (59-80% savings)
```bash
rtk git status          # Compact status
rtk git log             # Compact log (works with all git flags)
rtk git diff            # Compact diff (80%)
rtk git show            # Compact show (80%)
rtk git add             # Ultra-compact confirmations (59%)
rtk git commit          # Ultra-compact confirmations (59%)
rtk git push            # Ultra-compact confirmations
rtk git pull            # Ultra-compact confirmations
rtk git branch          # Compact branch list
rtk git fetch           # Compact fetch
rtk git stash           # Compact stash
rtk git worktree        # Compact worktree
```

Note: Git passthrough works for ALL subcommands, even those not explicitly listed.

### GitHub (26-87% savings)
```bash
rtk gh pr view <num>    # Compact PR view (87%)
rtk gh pr checks        # Compact PR checks (79%)
rtk gh run list         # Compact workflow runs (82%)
rtk gh issue list       # Compact issue list (80%)
rtk gh api              # Compact API responses (26%)
```

### JavaScript/TypeScript Tooling (70-90% savings)
```bash
rtk pnpm list           # Compact dependency tree (70%)
rtk pnpm outdated       # Compact outdated packages (80%)
rtk pnpm install        # Compact install output (90%)
rtk npm run <script>    # Compact npm script output
rtk npx <cmd>           # Compact npx command output
rtk prisma              # Prisma without ASCII art (88%)
```

### Files & Search (60-75% savings)
```bash
rtk ls <path>           # Tree format, compact (65%)
rtk read <file>         # Code reading with filtering (60%)
rtk grep <pattern>      # Search grouped by file (75%). Format flags (-c, -l, -L, -o, -Z) run raw.
rtk find <pattern>      # Find grouped by directory (70%)
```

### Analysis & Debug (70-90% savings)
```bash
rtk err <cmd>           # Filter errors only from any command
rtk log <file>          # Deduplicated logs with counts
rtk json <file>         # JSON structure without values
rtk deps                # Dependency overview
rtk env                 # Environment variables compact
rtk summary <cmd>       # Smart summary of command output
rtk diff                # Ultra-compact diffs
```

### Infrastructure (85% savings)
```bash
rtk docker ps           # Compact container list
rtk docker images       # Compact image list
rtk docker logs <c>     # Deduplicated logs
rtk kubectl get         # Compact resource list
rtk kubectl logs        # Deduplicated pod logs
```

### Network (65-70% savings)
```bash
rtk curl <url>          # Compact HTTP responses (70%)
rtk wget <url>          # Compact download output (65%)
```

### Meta Commands
```bash
rtk gain                # View token savings statistics
rtk gain --history      # View command history with savings
rtk discover            # Analyze Claude Code sessions for missed RTK usage
rtk proxy <cmd>         # Run command without filtering (for debugging)
rtk init                # Add RTK instructions to CLAUDE.md
rtk init --global       # Add RTK to ~/.claude/CLAUDE.md
```

## Token Savings Overview

| Category | Commands | Typical Savings |
|----------|----------|-----------------|
| Tests | vitest, playwright, cargo test | 90-99% |
| Build | next, tsc, lint, prettier | 70-87% |
| Git | status, log, diff, add, commit | 59-80% |
| GitHub | gh pr, gh run, gh issue | 26-87% |
| Package Managers | pnpm, npm, npx | 70-90% |
| Files | ls, read, grep, find | 60-75% |
| Infrastructure | docker, kubectl | 85% |
| Network | curl, wget | 65-70% |

Overall average: **60-90% token reduction** on common development operations.
