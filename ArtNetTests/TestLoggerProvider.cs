using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace ArtNetTests
{
    internal class TestLoggerProvider : ILoggerProvider
    {
        internal static readonly ILoggerProvider Instance = new TestLoggerProvider();

        private static readonly ConcurrentQueue<string> loggs = new ConcurrentQueue<string>();
        public ILogger CreateLogger(string categoryName)
        {
            Console.WriteLine($"CreateLogger {categoryName}");
            return new ConsoleLogger(categoryName);
        }

        public TestLoggerProvider()
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    if (loggs.TryDequeue(out var log))
                        Console.WriteLine(log);
                    await Task.Delay(1);
                }
            });
        }

        public void Dispose()
        {
        }
        private class ConsoleLogger : ILogger
        {
            private readonly string CategoryName;


            public ConsoleLogger(in string categoryName)
            {
                CategoryName = categoryName;
            }

            IDisposable? ILogger.BeginScope<TState>(TState state)
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception, string> formatter)
            {
                //_ = Task.Run(() =>
                //{
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine($"{DateTime.UtcNow} [{logLevel}] <{CategoryName}> {formatter?.Invoke(state, exception!)}");
                    if (exception != null)
                        stringBuilder.AppendLine(exception.ToString());

                    TestLoggerProvider.loggs.Enqueue(stringBuilder.ToString());
                //});
            }
        }
    }
}
