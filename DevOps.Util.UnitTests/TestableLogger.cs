using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace DevOps.Util.UnitTests
{
    public sealed class TestableLogger : ILogger
    {
        public ITestOutputHelper TestOutputHelper { get; set; }

        public TestableLogger(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper;
        }

        public IDisposable? BeginScope<TState>(TState state) => default;

        public bool IsEnabled(LogLevel logLevel) => TestOutputHelper is object;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            TestOutputHelper.WriteLine($"{logLevel} {eventId}: {formatter(state, exception)}");
        }
    }
}
