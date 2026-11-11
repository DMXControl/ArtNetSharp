using ArtNetSharp;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtNetTests;

public class LoggingTests
{
    [Test]
    public void TestLogging()
    {
        var logger = NullLoggerFactory.Instance.CreateLogger(nameof(LoggingTests));
        logger.LogTrace(new Exception("Test"));
        logger.LogDebug(new Exception("Test"));
        logger.LogInformation(new Exception("Test"));
        logger.LogWarning(new Exception("Test"));
        logger.LogError(new Exception("Test"));
        logger.LogCritical(new Exception("Test"));
        logger = ArtNetSharp.Logging.CreateLogger<LoggingTests>();
        logger.LogTrace(new Exception("Test"));
        logger.LogDebug(new Exception("Test"));
        logger.LogInformation(new Exception("Test"));
        logger.LogWarning(new Exception("Test"));
        logger.LogError(new Exception("Test"));
        logger.LogCritical(new Exception("Test"));
    }

}