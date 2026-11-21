using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ArtNetTests;

/// <summary>
/// The public send API of an instance (SendArtTimeCode/TimeSync/Sync/Address/IpProg/Nzs, sendArtDMX per IP),
/// sent by a controller and received by a node in the same ArtNet via the loop adapter.
/// </summary>
[NonParallelizable]
public class SendApiTest
{
    private static readonly IPv4Address nodeIp = new IPv4Address("3.3.3.3"); // the loop adapter ignores the IP
    private static readonly PortAddress universe = new PortAddress(0, 0, 13);
    private ArtNet artNet;
    private CapturingNode node;
    private ControllerInstanceMock controller;

    /// <summary>Node that records packets the library hands to OnPacketReceived (e.g. ArtNzs).</summary>
    private sealed class CapturingNode : NodeInstance
    {
        public readonly ConcurrentQueue<AbstractArtPacketCore> Unhandled = new();
        public CapturingNode(ArtNet artNet) : base(artNet) { }
        protected override void OnPacketReceived(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp)
        {
            Unhandled.Enqueue(packet);
            base.OnPacketReceived(packet, localIp, sourceIp);
        }
    }

    [SetUp]
    public void SetUp()
    {
        artNet = new ArtNet();
        artNet.LoopNetwork = new ArtNet.NetworkLoopAdapter(new IPv4Address("255.0.0.0"));
        foreach (var nic in artNet.NetworkClients)
            nic.Enabled = false;
        node = new CapturingNode(artNet) { Name = "Send API Node" };
        node.AddPortConfig(new PortConfig(1, universe, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet });
        controller = new ControllerInstanceMock(artNet, 0x4251) { Name = "Send API Controller" };
        artNet.AddInstance(node, controller);
    }

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    private static async Task<bool> within(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(10);
        return condition();
    }

    [Test]
    public async Task TimeCodeBroadcastAndUnicast()
    {
        var frames = new ConcurrentQueue<byte>();
        node.TimeCodeReceived += (_, tc) => frames.Enqueue(tc.Frames);

        await controller.SendArtTimeCode(new ArtTimeCode(0, 5, 1, 2, 3, ETimecodeType.EBU));
        await controller.SendArtTimeCode(new ArtTimeCode(0, 6, 1, 2, 3, ETimecodeType.EBU), nodeIp, broadcast: false);

        Assert.That(await within(() => frames.Count == 2), Is.True);
        Assert.That(frames, Is.EquivalentTo(new byte[] { 5, 6 }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => controller.SendArtTimeCode(new ArtTimeCode(0, 7, 1, 2, 3, ETimecodeType.EBU), null, broadcast: false),
            "Unicast without an IP address");
    }

    [Test]
    public async Task TimeSyncBroadcastAndUnicast()
    {
        var received = new ConcurrentQueue<ArtTimeSync>();
        node.TimeSyncReceived += (_, ts) => received.Enqueue(ts);
        var time = new DateTime(2026, 11, 21, 18, 30, 15);

        await controller.SendArtTimeSync(new ArtTimeSync(false, time));
        await controller.SendArtTimeSync(new ArtTimeSync(false, time), nodeIp, broadcast: false);

        Assert.That(await within(() => received.Count == 2), Is.True);
        Assert.That(received.First().DateTime, Is.EqualTo(time));
        await Assert.ThrowsAsync<ArgumentNullException>(() => controller.SendArtTimeSync(new ArtTimeSync(false, time), null, broadcast: false));
    }

    [Test]
    public async Task ArtSyncIsSent()
    {
        int syncs = 0;
        node.SyncReceived += (_, _) => Interlocked.Increment(ref syncs);
        await controller.SendArtSync();
        Assert.That(await within(() => Volatile.Read(ref syncs) >= 1), Is.True);
    }

    [Test]
    public async Task ArtAddressFromControllerReprogramsTheNode()
    {
        await controller.SendArtAddress(ArtAddress.CreateSetName(1, "Remote", "Remote Long Name"), nodeIp);
        Assert.That(await within(() => node.Name == "Remote Long Name"), Is.True);
        Assert.That(node.ShortName, Is.EqualTo("Remote"));
    }

    [Test]
    public async Task NodesDoNotSendArtAddressOrArtIpProg()
    {
        // Only controllers / config tools send these (EstCodes check in the library).
        var otherNode = new CapturingNode(artNet) { Name = "Other Node" };
        artNet.AddInstance(otherNode);
        int nodeAddresses = 0, nodeIpProgs = 0;
        node.ArtAddressReceived += (_, _) => Interlocked.Increment(ref nodeAddresses);
        node.ArtIpProgReceived += (_, _) => Interlocked.Increment(ref nodeIpProgs);

        await otherNode.SendArtAddress(ArtAddress.CreateSetName(1, "X", "X"), nodeIp);
        await otherNode.SendArtIpProg(new ArtIpProg(nodeIp, new IPv4Address("255.0.0.0"), nodeIp, EArtIpProgCommand.None), nodeIp);
        await Task.Delay(300);

        Assert.That(nodeAddresses, Is.EqualTo(0));
        Assert.That(nodeIpProgs, Is.EqualTo(0));
        Assert.That(node.Name, Is.EqualTo("Send API Node"));
    }

    [Test]
    public async Task ArtIpProgFromControllerIsAnswered()
    {
        ArtIpProg? request = null;
        node.ArtIpProgReceived += (_, e) => request = e.Request;
        await controller.SendArtIpProg(new ArtIpProg(new IPv4Address("10.1.1.1"), new IPv4Address("255.0.0.0"), new IPv4Address("10.0.0.1"), EArtIpProgCommand.None), nodeIp);
        Assert.That(await within(() => request != null), Is.True);
        Assert.That(request!.Ip, Is.EqualTo(new IPv4Address("10.1.1.1")));
    }

    [Test]
    public async Task ArtNzsIsSent()
    {
        byte[] data = { 1, 2, 3, 4 };
        await controller.SendArtNzs(new ArtNzs(1, 0x17, universe.Net, universe.Address, data), nodeIp);
        Assert.That(await within(() => node.Unhandled.OfType<ArtNzs>().Any()), Is.True);
        var nzs = node.Unhandled.OfType<ArtNzs>().First();
        Assert.That(nzs.StartCode, Is.EqualTo(0x17));
        Assert.That(nzs.Data.Take(4), Is.EqualTo(data));
    }

    [Test]
    public async Task ArtDmxToAnIpAddress()
    {
        byte[] data = new byte[512];
        data[0] = 77;
        await controller.sendArtDMX(nodeIp, universe, 1, data, 1);
        Assert.That(await within(() => node.GetReceivedDMX(universe)?[0] == 77), Is.True);
    }
}
