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
    public void ManyInstancesAreAllAdded()
    {
        var nodes = Enumerable.Range(0, 200).Select(i => new NodeMock(artNet) { Name = $"N{i}" }).ToArray();
        artNet.AddInstance(nodes);
        Assert.That(artNet.Instances, Has.Count.EqualTo(nodes.Length));
    }
}
