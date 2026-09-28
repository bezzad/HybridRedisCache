# Tasks

## 1. Spike (R&D) - decide go / no-go

- [ ] 1.1 Write a throw-away console test against Redis 8 (Docker) that turns on `CLIENT TRACKING ... REDIRECT` for the SE.Redis subscriber connection and prints `__redis__:invalidate` messages; verify by running `SET`/`DEL` from `redis-cli` and seeing the messages
- [ ] 1.2 Try the same with `protocol=resp3` and check if SE.Redis surfaces `invalidate` push frames; write the result (yes/no + how) in design.md D1
- [ ] 1.3 Measure BCAST vs default mode (messages received, server memory from `MEMORY STATS`) with 100k keys; write numbers in design.md D2
- [ ] 1.4 Check `CLIENT TRACKING` on Azure Cache for Redis and AWS ElastiCache docs (or a test instance); record the answer in design.md Risks
- [ ] 1.5 Decide go / no-go and update proposal.md status; stop here on no-go

## 2. Option and startup (only on go)

- [ ] 2.1 Add the opt-in invalidation-mode option to `HybridCachingOptions` with key-space as default; verify with a unit test that default options keep the current behaviour
- [ ] 2.2 In client-tracking mode, skip `CONFIG SET` and enable tracking (`BCAST PREFIX`, `NOLOOP`, `REDIRECT`); on error log and continue; verify with container tests for both success and a refused command
- [ ] 2.3 Document the option in README "Server requirements"; verify the README section renders and names Redis 6+

## 3. Invalidation handling (only on go)

- [ ] 3.1 Handle `__redis__:invalidate` messages: remove each key from the local cache (null key = flush all); verify with a container test where instance B and `redis-cli` change a key cached by instance A
- [ ] 3.2 Skip `SelfWriteNotificationWindow` in this mode; verify with a test that set-then-read on one instance hits the local cache
- [ ] 3.3 On reconnect, clear the local cache and re-enable tracking with the new subscriber id; verify with a test that kills the connection (`CLIENT KILL`) and checks the next read comes from Redis

## 4. Integration check

- [ ] 4.1 Run the full suite (`dotnet test --solution src/HybridRedisCache.sln`) in both modes and run the benchmark to compare with key-space mode
