using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using static ArtNetSharp.Logging;

[assembly: InternalsVisibleTo("ArtNetTests")]
namespace ArtNetSharp
{
    public static class Logging
    {
        private static ILoggerFactory loggerFactory;
        public static ILoggerFactory LoggerFactory
        {
            get
            {
                if (loggerFactory == null)
                {
                    bool isTest = AppDomain.CurrentDomain.GetAssemblies()
                        .Any(a => a.FullName.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase));
                    loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create((builder) =>
                    {
                        FileProvider fp = isTest ? new FileProvider() : null;
#if Debug
                        fp ?= new FileProvider();
#endif
                        if (isTest)
                        {
                            builder.AddConsole();
                            builder.SetMinimumLevel(LogLevel.Trace);
                        }
                        if (fp != null)
                            builder.AddProvider(fp);
                    });
                }
                return loggerFactory;
            }
            set
            {
                if(loggerFactory!=null)
                    throw new InvalidOperationException("LoggerFactory is already set. It can only be set once.");
                loggerFactory = value;
            }
        }
        
        internal static ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
        internal static ILogger CreateLogger(Type type) => LoggerFactory.CreateLogger(type);
        internal static ILogger CreateLogger(string categoryName) => LoggerFactory.CreateLogger(categoryName);
        internal static void LogTrace(this ILogger logger, Exception exception)
        {
            logger.LogTrace(exception, string.Empty);
        }
        internal static void LogDebug(this ILogger logger, Exception exception)
        {
            logger.LogDebug(exception, string.Empty);
        }
        internal static void LogInformation(this ILogger logger, Exception exception)
        {
            logger.LogInformation(exception, string.Empty);
        }
        internal static void LogWarning(this ILogger logger, Exception exception)
        {
            logger.LogWarning(exception, string.Empty);
        }
        internal static void LogError(this ILogger logger, Exception exception)
        {
            logger.LogError(exception, string.Empty);
        }
        internal static void LogCritical(this ILogger logger, Exception exception)
        {
            logger.LogCritical(exception, string.Empty);
        }
    }
}