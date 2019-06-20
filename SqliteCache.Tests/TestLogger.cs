using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoSmart.SqliteCache.Tests
{
    class TestLogger<T> : ILogger<T>
    {
        public readonly struct VoidScope : IDisposable
        {
            public void Dispose() {}
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new VoidScope();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Console.WriteLine($"{logLevel} {eventId}: {formatter(state, exception)}");
        }
    }
}
