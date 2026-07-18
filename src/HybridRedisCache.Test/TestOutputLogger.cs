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
        try
        {
            outputHelper.WriteLine($"{categoryName} [{logLevel}]: {formatter(state, exception)}");
        }
        catch (InvalidOperationException)
        {
            // The Redis multiplexer logs from its own threads and can emit after the test that owns
            // this ITestOutputHelper has finished, which makes xunit throw "There is no currently
            // active test". That escapes as an unhandled exception on a background thread and aborts
            // the whole run, so late output is dropped instead.
        }
    }
}
