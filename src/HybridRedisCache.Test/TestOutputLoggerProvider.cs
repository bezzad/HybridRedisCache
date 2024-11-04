using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

public class TestOutputLoggerProvider(ITestOutputHelper outputHelper) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TestOutputLogger(outputHelper, categoryName);

    public void Dispose() { }
}