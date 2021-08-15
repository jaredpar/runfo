using DevOps.Util.DotNet.Triage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public sealed class DatabaseLoggerOptions
    {
        public void Log(string output)
        {

        }
    }

    public sealed class DatabaseLoggerProvider : ILoggerProvider
    {
        public DatabaseLoggerOptions Options { get; }
        public DatabaseLogger Logger { get; }

        public DatabaseLoggerProvider(DatabaseLoggerOptions options)
        {
            Options = options;
            Logger = new(options);
        }

        public ILogger CreateLogger(string categoryName) => Logger;

        public void Dispose()
        {

        }
    }

    public sealed class DatabaseLogger : ILogger
    {
        public DatabaseLoggerOptions Options { get; }

        public DatabaseLogger(DatabaseLoggerOptions options)
        {
            Options = options;
        }

        public IDisposable? BeginScope<TState>(TState state) => default;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            Options.Log(message);
        }
    }
}
