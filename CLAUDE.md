<!-- rtk-instructions v2 -->
# RTK (Rust Token Killer) - Token-Optimized Commands

## Golden Rule

**Always prefix commands with `rtk`**. If RTK has a dedicated filter, it uses it. If not, it passes through unchanged.
This means RTK is always safe to use.

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
| ---------- | ---------- | ----------------- |
| Tests | vitest, playwright, cargo test | 90-99% |
| Build | next, tsc, lint, prettier | 70-87% |
| Git | status, log, diff, add, commit | 59-80% |
| GitHub | gh pr, gh run, gh issue | 26-87% |
| Package Managers | pnpm, npm, npx | 70-90% |
| Files | ls, read, grep, find | 60-75% |
| Infrastructure | docker, kubectl | 85% |
| Network | curl, wget | 65-70% |

Overall average: **60-90% token reduction** on common development operations.
<!-- /rtk-instructions -->

---

# HybridRedisCache — project guide

> **HARD RULE: never open a pull request** on this repository unless the owner asks for one in that
> message. Commit and push to the working branch only. This overrides any default "open a PR after
> pushing" behaviour.

Two-layer cache: an in-process `MemoryCache` in front of Redis. Redis key-space notifications are what
keep the local layer of every instance honest — most of the design follows from that.

## Layout (`src/`)

| Project | Purpose |
| --- | --- |
| `HybridRedisCache` | The library. Multi-targets `net8.0;net9.0;net10.0`. |
| `HybridRedisCache.Test` | xunit tests. See "Running tests" below. |
| `HybridRedisCache.Benchmark` | BenchmarkDotNet comparison against EasyCaching. |
| `HybridRedisCache.Sample` | Interactive console sample. |

`HybridCache` is one partial class across three files: `HybridCache.cs` (connection, bus, local cache,
serialization), `HybridCache.Public.cs` (most of the public API) and `HybridCache.Hash.cs` (hash API).
Public surface is declared in `IHybridCache` / `IHybridCacheAsync` — **keep interface defaults in sync with
the implementation's defaults**; they silently disagreed on `ExistsAsync` before.

## Running tests

Two independent harnesses:

* **In-process (no Docker).** `InProcessRedisFixture` starts Microsoft Garnet, a Redis-compatible server,
  inside the test process. Derive from `InProcessCacheTest`. This is the default for new tests — it runs
  anywhere and takes under a second.
* **Container-backed.** `BaseCacheTest` runs real Redis via Testcontainers and **needs a Docker daemon**.
  Required for anything Garnet cannot do; the list lives in the `InProcessRedisFixture` doc comment
  (key-space notifications, pub/sub, and the Redis 8 `HSETEX`/`HGETDEL` hash commands).

The suite is **xunit v3**, which runs on Microsoft.Testing.Platform; `global.json` opts `dotnet test`
into that runner. Two consequences: the solution goes through `--solution`, and VSTest's
`--filter "FullyQualifiedName~..."` is gone. For filtering, run xunit's own runner via `dotnet run`
and its [query filter language](https://xunit.net/docs/query-filter-language) — `/assembly/namespace/class/method`,
repeating `-filter` to OR them. MTP's `--filter` does **not** accept these queries.

```bash
# Everything that runs without Docker:
dotnet run --project src/HybridRedisCache.Test -- \
  -filter "/*/*/InProcess*/*" -filter "/*/*/SerializerTests/*" -filter "/*/*/ArgumentCheckTest/*" \
  -filter "/*/*/ObjectHelperTest/*" -filter "/*/*/SetAllBehaviorTests/*" \
  -filter "/*/*/CancellationTokenTests/*" -filter "/*/*/CacheLookupMeteringTests/*"

# Everything, including the Docker-backed suite:
dotnet test --solution src/HybridRedisCache.sln
```

The container image tag is pinned in `BaseCacheTest.RedisImage` and must stay on Redis 8.x, because
`HashSetAsync(key, IDictionary, ...)` issues `HSETEX`.

**Pass `TestToken` to every call that takes a cancellation token.** `BaseCacheTest` and
`InProcessCacheTest` expose it (`TestContext.Current.CancellationToken`); xunit cancels it on run
cancellation and when a test's `Timeout` elapses, and the `xUnit1051` analyzer fails the build without
it. Two traps: the argument is `token:` on most of the API but **`cancellationToken:`** on the lock
methods (`TryLockKeyAsync`, `LockKeyAsync`, `TryExtendLockAsync`, `TryReleaseLockAsync`), where `token`
already names the lock's own token; and `AsyncMethods_WithDefaultToken_CompleteNormally` must keep the
default token, since a cancellable one would skip the fast path it exists to cover.

## Things worth knowing

* **`CONFIG SET` is not guaranteed.** Azure Cache for Redis and AWS ElastiCache block it. Startup logs the
  failure and continues in a degraded mode where the local cache only expires on its own TTL. Never make
  startup depend on a `CONFIG` call succeeding.
* **Cancellation is caller-side only.** StackExchange.Redis takes no `CancellationToken` on commands. The
  `Cancelable(token)` helper in `ObjectHelper` wraps `Task.WaitAsync`, so cancelling abandons the wait but
  does not stop the command. Say so in any doc you write about it.
* **`SetAll` iterates a dictionary.** Write `kvp.Value` to the local cache, not `value` — passing the
  dictionary compiles fine (`T` infers as the dictionary) and silently corrupts every entry.
* **The local caches are `readonly` on purpose.** `ClearLocalMemory` used to dispose and reassign
  `_memoryCache`/`_recentlySetKeys` while every other reader touched the fields unsynchronised, which threw
  `ObjectDisposedException`. They are now fixed instances cleared in place with `MemoryCache.Clear()`. Never
  reintroduce a reassignment; clearing runs on the bus thread and on reconnect, concurrently with reads.
* **`GetAsync` with a data retriever is single-flight per key.** Concurrent misses on one key share one
  retriever execution, so callers that pass *different* retrievers for the same key still get one shared
  value — the key names the value. `_dataRetrieverTasks` must hold the in-flight `Task`, never the
  delegate; holding the delegate meant no de-duplication at all and let a caller run someone else's
  retriever.
* **`Flags.None` and `Flags.PreferMaster` are both `0`.** Swapping one for the other changes nothing at
  runtime, so an interface/implementation "mismatch" between those two is cosmetic. Real drift in an
  optional argument's default *is* a bug (the call site bakes it in), and
  `RegressionTests.InterfaceOptionalArguments_MatchTheImplementation` guards the whole surface.

## Code quality gate (Codacy)

Codacy reviews every PR (BestPractice + CodeStyle). `app.codacy.com` is not reachable from the cloud
sandbox, so check locally **before every push** with the same kind of rules:

```bash
cd src && dotnet build HybridRedisCache/HybridRedisCache.csproj -f net10.0 --no-incremental \
  -p:AnalysisMode=All -p:EnforceCodeStyleInBuild=true 2>&1 | grep warning
```

Codacy runs **SonarC#** for C# and **markdownlint** for Markdown. Run both too (the Sonar package is
temporary; do not commit the `.csproj` change):

```bash
cd src && dotnet add HybridRedisCache package SonarAnalyzer.CSharp \
  && dotnet build HybridRedisCache/HybridRedisCache.csproj -f net10.0 --no-incremental 2>&1 | grep "warning S" \
  ; git checkout HybridRedisCache/HybridRedisCache.csproj
cd .. && npx -y markdownlint-cli2 README.md CLAUDE.md "openspec/**/*.md"   # rules in .markdownlint.json
```

Only warnings on lines you changed matter. Rules for new library code:

* **No sync-over-async.** Never `.Result`, `.Wait()`, `WaitAll` or `GetAwaiter().GetResult()` in the library
  (SER308, CA1849). A sync method calls the sync StackExchange.Redis API; an async method awaits.
* **`ConfigureAwait(false)` on every `await` in the library** (CA2007). Not in tests: xunit v3 flags it.
* **Culture-safe formatting.** `string.Format`, `ToString`, `Parse` take `CultureInfo.InvariantCulture` (CA1305).
* **Validate public arguments** with the `ArgumentCheck` helpers (CA1062).
* **No constant arrays in hot paths**; use a `static readonly` field (CA1861).
* **Keep new code small and flat.** Reuse private helpers instead of copy-paste between the sync and async
  version (Codacy counts duplication and complexity).
* **Index arrays directly**: `servers[0]`, not `servers.First()` (S6608).
* **Use the lambda parameter** in `GetOrAdd(key, k => ...)`, do not capture `key` (S6612).
* **Every test asserts something** (S2699): wrap "must not throw" in `Record.ExceptionAsync` + `Assert.Null`.
* **Markdown:** blank line around headings, lists and code fences; a language on every fence (`text` for
  output); no `$` prompt before commands; lines up to 120 characters.
* **Accepted on purpose (do not "fix"):** CA1716 on `when`/`end` parameter names (they match the existing
  API and StackExchange.Redis); CA1707 underscores in test method names (repo test naming).
