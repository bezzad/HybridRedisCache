using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace HybridRedisCache.Test;

internal class LoggerMock : ILogger
{
    public List<object> LogHistory;

    public LoggerMock(string categoryName)
    {
        LogHistory = new List<object>();
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        // Not implemented in this example
        LogHistory.Add(state);
    }
}
