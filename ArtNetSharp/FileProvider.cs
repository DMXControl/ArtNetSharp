using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArtNetSharp
{
    internal class FileProvider : ILoggerProvider
    {
        private static readonly string fileDirectoryWindows = Path.Combine("C:", ".Debug", "ArtNetSharp");
        private static readonly string fileDirectoryLinux = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArtNetSharp");
        private static readonly string fileDirectoryMacOS = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArtNetSharp");
        private static readonly string fileDirectory = getOsDirectory();

        private static readonly string filePath = Path.Combine(fileDirectory, "log.txt");
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private static string getOsDirectory()
        {
            if (Tools.IsRunningOnGithubWorker())
            {
                var ad = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (ad.Contains("runner/work")) // Linux and Mac Worker
                    return ad;
                if (ad.Contains(":\\a\\")) // Windows Worker
                    return ad;
            }

            if (Tools.IsWindows())
                return fileDirectoryWindows;
            if (Tools.IsLinux())
                return fileDirectoryLinux;
            if (Tools.IsAndroid())
                return fileDirectoryLinux;
            if (Tools.IsMac())
                return fileDirectoryMacOS;

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
            catch
            {

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
                if (string.IsNullOrWhiteSpace(FileProvider.fileDirectory))
                    return;
                if (!Directory.Exists(fileDirectory))
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
                    catch(Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    finally { FileProvider.semaphore.Release(); }
                });
            }
        }
    }
}
