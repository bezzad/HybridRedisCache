using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

public class TestOutputLogger(ITestOutputHelper outputHelper, string categoryName) : ILogger
{
    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        outputHelper.WriteLine($"{categoryName} [{logLevel}]: {formatter(state, exception)}");
    }
}