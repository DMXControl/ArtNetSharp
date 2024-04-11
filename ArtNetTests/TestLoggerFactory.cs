﻿using Microsoft.Extensions.Logging;
using System.Text;

namespace ArtNetTests
{
    internal class TestLoggerFactory
    {
        internal static readonly ILoggerFactory Instance = new LoggerFactory([new TestLoggerProvider()]);
    }
    internal class TestLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new ConsoleLogger(categoryName);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
        private class ConsoleLogger : ILogger
        {
            private readonly string CategoryName;


            public ConsoleLogger(in string categoryName)
            {
                CategoryName = categoryName;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public async void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"{DateTime.UtcNow} [{logLevel}] <{CategoryName}> {formatter?.Invoke(state, exception)}");
                if (exception != null)
                    stringBuilder.AppendLine(exception.ToString());

                Console.WriteLine(stringBuilder.ToString());
            }
        }
    }
}