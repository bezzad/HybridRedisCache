using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HybridRedisCache.Test;

/// <summary>
/// Guards for defects found by review. Each test here failed before its fix.
/// </summary>
public class RegressionTests(InProcessRedisFixture fixture, ITestOutputHelper output)
    : InProcessCacheTest(fixture, output)
{
    /// <summary>
    /// The data retriever used to be stored as a delegate rather than as its in-flight task, so every
    /// concurrent caller ran it (20 executions for 20 readers) instead of sharing one result.
    /// </summary>
    [Fact]
    public async Task GetWithDataRetriever_RunsTheRetrieverOnce_ForConcurrentCallersOfOneKey()
    {
        var key = UniqueKey;
        var calls = 0;

        async Task<string> Retriever(string _)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(300, TestToken);
            return "value";
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Cache.GetAsync(key, Retriever, TimeSpan.FromMinutes(1), token: TestToken)));

        Assert.All(results, r => Assert.Equal("value", r));
        Assert.Equal(1, Volatile.Read(ref calls));
    }

    /// <summary>
    /// Sharing one key means sharing one retrieval: the winner's value is what every caller gets.
    /// Previously the loser executed the winner's delegate *and* its own, so the retriever ran twice.
    /// </summary>
    [Fact]
    public async Task GetWithDataRetriever_SharesOneResult_WhenCallersPassDifferentRetrievers()
    {
        var key = UniqueKey;
        var calls = 0;

        Func<string, Task<string>> Make(string label) => async _ =>
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(300, TestToken);
            return label;
        };

        var a = Cache.GetAsync(key, Make("from-A"), TimeSpan.FromMinutes(1), token: TestToken);
        var b = Cache.GetAsync(key, Make("from-B"), TimeSpan.FromMinutes(1), token: TestToken);
        var results = await Task.WhenAll(a, b);

        Assert.Equal(results[0], results[1]);
        Assert.Contains(results[0], new[] { "from-A", "from-B" });
        Assert.Equal(1, Volatile.Read(ref calls));
    }

    /// <summary>
    /// A retriever failure must surface to every caller waiting on it, and must not poison the key:
    /// the next call gets a fresh attempt.
    /// </summary>
    [Fact]
    public async Task GetWithDataRetriever_FailureIsNotCached()
    {
        var key = UniqueKey;
        var attempts = 0;

        async Task<string> Failing(string _)
        {
            Interlocked.Increment(ref attempts);
            await Task.Delay(50, TestToken);
            throw new InvalidOperationException("retriever blew up");
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Cache.GetAsync(key, Failing, TimeSpan.FromMinutes(1), token: TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Cache.GetAsync(key, Failing, TimeSpan.FromMinutes(1), token: TestToken));

        Assert.Equal(2, Volatile.Read(ref attempts));
    }

    /// <summary>
    /// Clearing the local cache used to dispose both MemoryCache instances and reassign the fields
    /// while readers used them without a lock, which threw ObjectDisposedException.
    /// </summary>
    [Fact]
    public async Task ClearingLocalCache_ConcurrentlyWithReads_DoesNotThrow()
    {
        var key = UniqueKey;
        await Cache.SetAsync(key, "value", TimeSpan.FromMinutes(5), token: TestToken);

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                Cache.TryGetValue(key, out string _);
                await Cache.SetAsync(key, "value", TimeSpan.FromMinutes(5), token: TestToken);
            }
        }, TestToken)).ToArray();

        var clearer = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                await Cache.FlushLocalCachesAsync(TestToken);
                await Task.Delay(5, TestToken);
            }
        }, TestToken);

        // Any ObjectDisposedException from the racing reads surfaces here.
        var error = await Record.ExceptionAsync(() => Task.WhenAll(readers.Append(clearer)));
        Assert.Null(error);
    }

    /// <summary>
    /// An optional argument's default is baked in at the call site, so when the interface and the
    /// implementation disagree the behaviour depends on the static type of the reference — calling
    /// through <see cref="IHybridCache"/> would do something different from calling the class.
    /// `ExistsAsync` drifted this way before, so the whole surface is checked here.
    /// </summary>
    /// <remarks>
    /// The interface map pairs each interface method with the method that actually implements it,
    /// which a name-and-signature lookup cannot do for the generic members.
    /// </remarks>
    [Theory]
    [InlineData(typeof(IHybridCache))]
    [InlineData(typeof(IHybridCacheAsync))]
    public void InterfaceOptionalArguments_MatchTheImplementation(Type contract)
    {
        var mismatches = new List<string>();
        var map = typeof(HybridCache).GetInterfaceMap(contract);

        foreach (var (declared, implemented) in map.InterfaceMethods.Zip(map.TargetMethods))
        {
            foreach (var (want, got) in declared.GetParameters().Zip(implemented.GetParameters()))
            {
                if (!want.HasDefaultValue && !got.HasDefaultValue)
                    continue;

                if (want.HasDefaultValue != got.HasDefaultValue ||
                    !Equals(want.DefaultValue, got.DefaultValue))
                {
                    mismatches.Add(
                        $"{contract.Name}.{declared.Name}({want.Name}): " +
                        $"interface={Describe(want)} implementation={Describe(got)}");
                }
            }
        }

        // Taken from the ambient context rather than the constructor parameter, which the base class
        // already owns (capturing it here as well trips CS9107).
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{contract.Name}: checked {map.InterfaceMethods.Length} members");
        Assert.Empty(mismatches);
        return;

        static string Describe(ParameterInfo p) => p.HasDefaultValue ? $"{p.DefaultValue ?? "null"}" : "<none>";
    }
}
