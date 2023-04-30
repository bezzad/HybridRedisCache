using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;

namespace HybridRedisCache.Test;

internal class LoggerFactoryMock : ILoggerFactory
{
    private readonly ConcurrentDictionary<string, ILogger> _loggers;

    public LoggerFactoryMock()
    {
        _loggers = new ConcurrentDictionary<string, ILogger>();
    }

    public void AddProvider(ILoggerProvider provider)
    {
        // Not implemented in this example
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new LoggerMock(name));
    }

    public void Dispose()
    {
        foreach (var logger in _loggers.Values)
        {
            (logger as IDisposable)?.Dispose();
        }
        _loggers.Clear();
    }
}
