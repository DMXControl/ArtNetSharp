using ArtNetSharp;
using ArtNetTests.Mocks.Instances;

namespace ArtNetTests;

public class InstanceManagementTest
{
    private ArtNet artNet;

    [SetUp]
    public void SetUp() => artNet = new ArtNet();

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    [Test]
    public void AddInstanceSkipsOnlyAlreadyAddedInstances()
    {
        var a = new NodeMock(artNet) { Name = "A" };
        var b = new NodeMock(artNet) { Name = "B" };
        var c = new NodeMock(artNet) { Name = "C" };

        artNet.AddInstance(a);
        artNet.AddInstance(a, b, null!, c); // a is already there, b and c must still be added

        Assert.That(artNet.Instances, Is.EquivalentTo(new[] { a, b, c }));
    }

    [Test]
    public void RemoveInstanceSkipsOnlyUnknownInstances()
    {
        var a = new NodeMock(artNet) { Name = "A" };
        var b = new NodeMock(artNet) { Name = "B" };
        var c = new NodeMock(artNet) { Name = "C" };
        var notAdded = new NodeMock(artNet) { Name = "Not added" };
        artNet.AddInstance(a, b, c);

        artNet.RemoveInstance(notAdded, a, null!, b); // notAdded is unknown, a and b must still be removed

        Assert.That(artNet.Instances, Is.EquivalentTo(new[] { c }));
    }

    [Test]
    public void IsDeactivatedFollowsAddAndRemove()
    {
        var a = new NodeMock(artNet) { Name = "A" };
        Assert.That(a.IsDeactivated, Is.True, "not added yet");
        artNet.AddInstance(a);
        Assert.That(a.IsDeactivated, Is.False, "added");
        artNet.RemoveInstance(a);
        Assert.That(a.IsDeactivated, Is.True, "removed");

        using var otherArtNet = new ArtNet();
        otherArtNet.AddInstance(a);
        Assert.That(a.IsDeactivated, Is.True, "added to an ArtNet it was not created for");
        otherArtNet.RemoveInstance(a);
    }

    [Test]
    public void EachInstanceLogsUnderItsOwnType()
    {
        var capture = new CategoryCapture();
        ArtNet.AddLoggProvider(capture);

        var node = new NodeMock(artNet) { Name = "Logging Node" };
        _ = new ArtNetTests.Mocks.ControllerInstanceMock(artNet) { Name = "Created later" }; // used to overwrite the static logger
        ((IDisposable)node).Dispose();

        Assert.That(capture.CategoryOf("Disposing Logging Node"), Does.EndWith(nameof(NodeMock)));
    }

    private sealed class CategoryCapture : Microsoft.Extensions.Logging.ILoggerProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<(string Category, string Message)> entries = new();
        public string? CategoryOf(string message) => entries.LastOrDefault(e => e.Message == message).Category;
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new Capturing(categoryName, entries);
        public void Dispose() { }

        private sealed class Capturing(string category, System.Collections.Concurrent.ConcurrentQueue<(string, string)> entries) : Microsoft.Extensions.Logging.ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            // The provider stays registered for the rest of the run, so keep it cheap.
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => logLevel >= Microsoft.Extensions.Logging.LogLevel.Information;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                    entries.Enqueue((category, formatter(state, exception)));
            }
        }
    }

    [Test]
    public void ManyInstancesAreAllAdded()
    {
        var nodes = Enumerable.Range(0, 200).Select(i => new NodeMock(artNet) { Name = $"N{i}" }).ToArray();
        artNet.AddInstance(nodes);
        Assert.That(artNet.Instances, Has.Count.EqualTo(nodes.Length));
    }
}
