using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using ArtNetTests.Mocks.Instances;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ArtNetTests;

/// <summary>
/// RemoteClient lifecycle seen by a controller (loop adapter): discovery, timeout when the node stops replying
/// (RemoteClientTimedOut), and revival as the same object when it comes back. Plus ConfigInstance, which discovers
/// via broadcast ArtPoll, processes ArtDataReply and may reprogram nodes.
/// </summary>
[NonParallelizable]
public class RemoteClientLifecycleTest
{
    private static readonly PortAddress universe = new PortAddress(0, 0, 14);
    private static readonly IPv4Address loopIp = new IPv4Address("3.3.3.3");
    private ArtNet artNet;
    private NodeMock node;
    private TimeSpan originalClientTimeout, originalCheckDelay;

    [SetUp]
    public void SetUp()
    {
        originalClientTimeout = RemoteClient.Timeout;
        originalCheckDelay = AbstractInstance.TimeoutCheckDelay;
        artNet = new ArtNet();
        artNet.LoopNetwork = new ArtNet.NetworkLoopAdapter(new IPv4Address("255.0.0.0"));
        foreach (var nic in artNet.NetworkClients)
            nic.Enabled = false;
        node = new NodeMock(artNet) { Name = "Lifecycle Node" };
        node.AddPortConfig(new PortConfig(1, universe, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet });
    }

    [TearDown]
    public void TearDown()
    {
        ((IDisposable)artNet)?.Dispose();
        RemoteClient.Timeout = originalClientTimeout;
        AbstractInstance.TimeoutCheckDelay = originalCheckDelay;
    }

    private static async Task<bool> within(Func<bool> condition, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(20);
        return condition();
    }

    [Test, CancelAfter(30_000)]
    public async Task NodeTimesOutAndIsRevivedAsTheSameObject()
    {
        // Longer than the poll period (2.7 s), so a node that keeps replying never times out.
        RemoteClient.Timeout = TimeSpan.FromSeconds(3.5);
        AbstractInstance.TimeoutCheckDelay = TimeSpan.FromMilliseconds(100);

        var controller = new ControllerInstanceMock(artNet, 0x4252) { Name = "Lifecycle Controller" };
        controller.AddPortConfig(new PortConfig(1, universe, false, true) { PortNumber = 1, Type = EPortType.InputToArtNet });
        var discovered = new ConcurrentQueue<RemoteClient>();
        var timedOut = new ConcurrentQueue<RemoteClient>();
        controller.RemoteClientDiscovered += (_, rc) => discovered.Enqueue(rc);
        controller.RemoteClientTimedOut += (_, rc) => timedOut.Enqueue(rc);
        artNet.AddInstance(node, controller);

        RemoteClient? client = null;
        Assert.That(await within(() => (client = controller.RemoteClients?.FirstOrDefault(rc => rc.LongName == "Lifecycle Node")) != null, 5000), Is.True);

        artNet.RemoveInstance(node); // the node stops replying
        Assert.That(await within(() => timedOut.Any(rc => ReferenceEquals(rc, client)), 12_000), Is.True, "RemoteClientTimedOut");
        Assert.That(controller.RemoteClients.Any(rc => rc.LongName == "Lifecycle Node"), Is.False);

        artNet.AddInstance(node); // back again
        Assert.That(await within(() => controller.RemoteClients.Any(rc => rc.LongName == "Lifecycle Node"), 8000), Is.True);
        Assert.That(controller.RemoteClients.First(rc => rc.LongName == "Lifecycle Node"), Is.SameAs(client), "revived as the same object");
        Assert.That(discovered.Count(rc => ReferenceEquals(rc, client)), Is.EqualTo(2), "RemoteClientDiscovered on discovery and on revival");
    }

    [Test]
    public async Task ConfigInstanceDiscoversProcessesArtDataAndReprograms()
    {
        var config = new ConfigInstance(artNet) { Name = "Config Tool" };
        artNet.AddInstance(node, config);

        // A config tool polls by broadcast (no targeted mode), so it sees the node without own ports.
        RemoteClient? client = null;
        Assert.That(await within(() => (client = config.RemoteClients?.FirstOrDefault(rc => rc.LongName == "Lifecycle Node")) != null, 8000), Is.True);

        // ArtDataReply from the node is stored in the RemoteClient's ArtData cache.
        using (var reply = new ArtDataReply(0xffff, 0xffff, EDataRequest.UrlProduct, "https://example.invalid/product"))
            ((IInstance)config).PacketReceived(reply, loopIp, loopIp);
        Assert.That(await within(() => client!.ArtDataCache.ContainsKey(EDataRequest.UrlProduct), 2000), Is.True);
        Assert.That(client!.ArtDataCache[EDataRequest.UrlProduct], Is.EqualTo("https://example.invalid/product"));

        // Config tools may reprogram nodes.
        await config.SendArtAddress(ArtAddress.CreateSetName(1, "Configured", "Configured Node"), loopIp);
        Assert.That(await within(() => node.Name == "Configured Node", 2000), Is.True);

        // RDM discovery entry point (the node has no RDM, so nothing is requested - must not throw).
        await Assert.DoesNotThrowAsync(() => config.PerformRDMDiscovery());
    }
}
