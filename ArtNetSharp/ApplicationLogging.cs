using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
        internal static readonly ILoggerFactory LoggerFactory = new LoggerFactory();
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
    }
}