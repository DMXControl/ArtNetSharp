using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;
using static ArtNetSharp.ApplicationLogging;

[assembly: InternalsVisibleTo("ArtNetTests")]
namespace ArtNetSharp
{
    /// <summary>
    /// Shared logger
    /// </summary>
    internal static class ApplicationLogging
    {
        internal static readonly ILoggerFactory LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            if (Tools.IsRunningOnGithubWorker())
                builder.AddConsole();
        });
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