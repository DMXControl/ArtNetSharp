using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ArtNetSharp.ApplicationLogging;

namespace ArtNetSharp
{
    /// <summary>
    /// Shared logger
    /// </summary>
    internal static class ApplicationLogging
    {
        internal static ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;
        internal static ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
        internal static ILogger CreateLogger(Type type) => LoggerFactory.CreateLogger(type);
        internal static ILogger CreateLogger(string categoryName) => LoggerFactory.CreateLogger(categoryName);
        public static void LogTrace(this ILogger logger, Exception exception)
        {
            logger.LogTrace(exception, string.Empty);
        }
        public static void LogDebug(this ILogger logger, Exception exception)
        {
            logger.LogDebug(exception, string.Empty);
        }
        public static void LogInformation(this ILogger logger, Exception exception)
        {
            logger.LogInformation(exception, string.Empty);
        }
        public static void LogWarning(this ILogger logger, Exception exception)
        {
            logger.LogWarning(exception, string.Empty);
        }
        public static void LogError(this ILogger logger, Exception exception)
        {
            logger.LogError(exception, string.Empty);
        }
        public static void LogCritical(this ILogger logger, Exception exception)
        {
            logger.LogCritical(exception, string.Empty);
        }

        internal class FileProvider : ILoggerProvider
        {
            private static string fileDirectoryWindows = Path.Combine("C:", ".Debug", "ArtNetSharp");
            private static string fileDirectoryLinux = "/var/log/ArtNetSharp";
            private static string fileDirectory = getOsDirectory();

            private static string filePath = Path.Combine(fileDirectory, "log.txt");
            private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

            private static string getOsDirectory()
            {
                if (OperatingSystem.IsWindows())
                    return fileDirectoryWindows;
                if (OperatingSystem.IsLinux())
                    return fileDirectoryLinux;
                if (OperatingSystem.IsAndroid())
                    return fileDirectoryLinux;

                return null;
            }
            public FileProvider()
            {
                if (string.IsNullOrWhiteSpace(fileDirectory))
                    return;
                FileProvider.semaphore.WaitAsync();
                try
                {
                    if (!Directory.Exists(fileDirectory))
                        Directory.CreateDirectory(fileDirectory);

                    if (File.Exists(filePath))
                        File.Delete(filePath);

                    using (var file = File.Create(filePath))
                    {

                    }
                }
                finally { FileProvider.semaphore.Release(); }
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new TextLogger(this, categoryName.Split('.').Last());
            }

            public void Dispose()
            {
            }

            private class TextLogger : ILogger
            {
                private readonly string CategoryName;
                private readonly FileProvider Provider;


                public TextLogger(FileProvider provider, in string categoryName)
                {
                    CategoryName = categoryName;
                    Provider = provider;
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
                    if (string.IsNullOrWhiteSpace(fileDirectory))
                        return;

                    await Task.Run(async () =>
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        stringBuilder.AppendLine($"{DateTime.UtcNow} [{logLevel}] <{CategoryName}> {formatter?.Invoke(state, exception)}");
                        if (exception != null)
                            stringBuilder.AppendLine(exception.ToString());
                        await FileProvider.semaphore.WaitAsync();
                        try
                        {
                            File.AppendAllText(FileProvider.filePath, stringBuilder.ToString());
                        }
                        finally { FileProvider.semaphore.Release(); }
                    });
                }
            }
        }
    }
}
