using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks.Instances;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ArtNetTests;

/// <summary>
/// ArtIpProg: the node answers with ArtIpProgReply. Without a handler it reports its current settings unchanged;
/// an application handler can apply the request and report the new settings. Replies travel over the loop adapter.
/// </summary>
public class NodeIpProgrammingTest
{
    private static readonly IPv4Address localIp = new IPv4Address("127.0.0.1");
    private static readonly IPv4Address controllerIp = new IPv4Address("127.0.0.1");
    private ArtNet artNet;
    private NodeMock node;
    private ReplyCollector collector;

    /// <summary>Records the ArtIpProgReply packets it receives.</summary>
    private sealed class ReplyCollector : ControllerInstance
    {
        public readonly ConcurrentQueue<ArtIpProgReply> Replies = new();
        public ReplyCollector(ArtNet artNet) : base(artNet) { }
        protected override void OnPacketReceived(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp)
        {
            if (packet is ArtIpProgReply reply)
                Replies.Enqueue(reply);
            base.OnPacketReceived(packet, localIp, sourceIp);
        }
    }

    [SetUp]
    public void SetUp()
    {
        artNet = new ArtNet();
        artNet.LoopNetwork = new ArtNet.NetworkLoopAdapter(new IPv4Address("255.0.0.0"));
        // The loop adapter replaces sending only; ignore real traffic from other software on this machine.
        foreach (var nic in artNet.NetworkClients)
            nic.Enabled = false;
        node = new NodeMock(artNet) { Name = "IpProg Node" };
        collector = new ReplyCollector(artNet) { Name = "IpProg Collector" };
        artNet.AddInstance(node, collector);
    }

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    private async Task<ArtIpProgReply?> waitForReply()
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3000)
        {
            if (collector.Replies.TryDequeue(out var reply))
                return reply;
            await Task.Delay(10);
        }
        return null;
    }

    private void send(AbstractInstance instance, ArtIpProg request)
    {
        using (request)
            ((IInstance)instance).PacketReceived(request, localIp, controllerIp);
    }

    [Test]
    public async Task WithoutHandlerTheCurrentSettingsAreReported()
    {
        send(node, new ArtIpProg(new IPv4Address("10.9.8.7"), new IPv4Address("255.255.0.0"), new IPv4Address("10.9.0.1"),
                                 EArtIpProgCommand.EnableProgramming | EArtIpProgCommand.ProgramIP));
        var reply = await waitForReply();

        Assert.That(reply, Is.Not.Null, "Spec: the node replies with ArtIpProgReply");
        Assert.That(reply!.Ip, Is.EqualTo(localIp), "Nothing was applied, so the current IP is reported");
        Assert.That(reply.SubnetMask, Is.EqualTo(new IPv4Address("255.0.0.0")), "Mask of the receiving interface (loopback)");
    }

    [Test]
    public async Task HandlerCanApplyAndReportNewSettings()
    {
        ArtIpProg? received = null;
        node.ArtIpProgReceived += (_, e) =>
        {
            received = e.Request;
            // e.g. an embedded device applies the settings here
            e.Ip = e.Request.Ip;
            e.SubnetMask = e.Request.SubnetMask;
            e.DefaultGateway = e.Request.DefaultGateway;
            e.DhcpEnabled = false;
        };

        send(node, new ArtIpProg(new IPv4Address("10.9.8.7"), new IPv4Address("255.255.0.0"), new IPv4Address("10.9.0.1"),
                                 EArtIpProgCommand.EnableProgramming | EArtIpProgCommand.ProgramIP | EArtIpProgCommand.ProgramSubnetMask));
        var reply = await waitForReply();

        Assert.That(received, Is.Not.Null);
        Assert.That(reply, Is.Not.Null);
        Assert.That(reply!.Ip, Is.EqualTo(new IPv4Address("10.9.8.7")));
        Assert.That(reply.SubnetMask, Is.EqualTo(new IPv4Address("255.255.0.0")));
        Assert.That(reply.DefaultGateway, Is.EqualTo(new IPv4Address("10.9.0.1")));
    }

    [Test]
    public async Task ControllerDoesNotReply()
    {
        send(collector, new ArtIpProg(new IPv4Address("10.9.8.7"), new IPv4Address("255.255.0.0"), new IPv4Address("10.9.0.1"), EArtIpProgCommand.None));
        Assert.That(await waitForReply(), Is.Null, "Spec: controllers take no action");
    }
}
