using ArtNetSharp;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtNetTests.Logging
{
    public class LoggingTests
    {
        [Test]
        public void TestSendDMXLoopOverNetwork()
        {
            var logger = NullLoggerFactory.Instance.CreateLogger(nameof(LoggingTests));
            logger.LogTrace(new Exception("Test"));
            logger.LogDebug(new Exception("Test"));
            logger.LogInformation(new Exception("Test"));
            logger.LogWarning(new Exception("Test"));
            logger.LogError(new Exception("Test"));
            logger.LogCritical(new Exception("Test"));
        }

    }
}