using ArtNetSharp;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtNetTests.Logging
{
    public class LoggingTests
    {
        [Test, Order(-1)]
        public void TestLogging()
        {
            var logger = NullLoggerFactory.Instance.CreateLogger(nameof(LoggingTests));
            logger.LogTrace(new Exception("Test"));
            logger.LogDebug(new Exception("Test"));
            logger.LogInformation(new Exception("Test"));
            logger.LogWarning(new Exception("Test"));
            logger.LogError(new Exception("Test"));
            logger.LogCritical(new Exception("Test"));
            logger = ApplicationLogging.CreateLogger<LoggingTests>();
            logger.LogTrace(new Exception("Test"));
            logger.LogDebug(new Exception("Test"));
            logger.LogInformation(new Exception("Test"));
            logger.LogWarning(new Exception("Test"));
            logger.LogError(new Exception("Test"));
            logger.LogCritical(new Exception("Test"));
        }

    }
}