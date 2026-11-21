using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetSharp.Misc;
using ArtNetTests.Mocks;
using ArtNetTests.Mocks.Instances;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ArtNetTests;

/// <summary>
/// ArtDiagData: controllers request diagnostics via ArtPoll (Flags bit 2/3, DiagPriority); nodes send them with
/// SendDiagnostics to those controllers only, filtered by priority (several controllers: broadcast, lowest minimum).
/// </summary>
[NonParallelizable]
public class DiagnosticsTest
{
    private static readonly PortAddress universe = new PortAddress(0, 1, 1);
    private static readonly IPv4Address loopIp = new IPv4Address("3.3.3.3");
    private ArtNet artNet;
    private NodeMock node;

    [SetUp]
    public void SetUp()
    {
        artNet = new ArtNet();
        artNet.LoopNetwork = new ArtNet.NetworkLoopAdapter(new IPv4Address("255.0.0.0"));
        foreach (var nic in artNet.NetworkClients)
            nic.Enabled = false;
        node = new NodeMock(artNet) { Name = "Diag Node" };
        node.AddPortConfig(new PortConfig(1, universe, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet });
        artNet.AddInstance(node);
    }

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    private void pollFrom(IPv4Address controllerIp, EArtPollFlags flags, EPriorityCode priority = EPriorityCode.None)
    {
        using var artPoll = new ArtPoll(0x4255, 0x0001, flags: flags, priority: priority);
        ((IInstance)node).PacketReceived(artPoll, loopIp, controllerIp);
    }

    private static async Task<bool> within(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(10);
        return condition();
    }

    [Test]
    public void PacketRoundTrip()
    {
        using var created = new ArtDiagData("Port 2 output short circuit", EPriorityCode.DpHigh, 2);
        byte[] bytes = created.GetPacket();
        Assert.That(bytes[16] << 8 | bytes[17], Is.EqualTo("Port 2 output short circuit".Length + 1), "Length includes the null terminator");
        Assert.That(ArtNetSharp.Tools.TryDeserializePacket(bytes, out var packet), Is.True);
        Assert.That(packet, Is.EqualTo(created));
        var parsed = (ArtDiagData)packet;
        Assert.Multiple(() =>
        {
            Assert.That(parsed.Text, Is.EqualTo("Port 2 output short circuit"));
            Assert.That(parsed.Priority, Is.EqualTo(EPriorityCode.DpHigh));
            Assert.That(parsed.LogicalPort, Is.EqualTo(2));
        });
        using var tooLong = new ArtDiagData(new string('x', 600));
        Assert.That(tooLong.Text, Has.Length.EqualTo(ArtDiagData.MaxTextLength), "max 512 bytes incl. null");
        Assert.That(tooLong.GetPacket(), Has.Length.EqualTo(18 + 512));
    }

    [Test]
    public async Task NothingIsSentWithoutARequest()
    {
        Assert.That(await node.SendDiagnostics("nobody asked"), Is.False);
        pollFrom(new IPv4Address("10.0.0.2"), EArtPollFlags.None);
        Assert.That(await node.SendDiagnostics("still nobody"), Is.False);
    }

    [Test]
    public async Task ControllerRequestsAndReceivesDiagnostics()
    {
        // A real controller whose own ArtPolls carry the request (it polls its input range, so it needs an input port).
        var controller = new ControllerInstanceMock(artNet, 0x4256) { Name = "Diag Controller", RequestDiagnostics = true, DiagnosticsMinimumPriority = EPriorityCode.DpMed };
        controller.AddPortConfig(new PortConfig(1, universe, false, true) { PortNumber = 1, Type = EPortType.InputToArtNet });
        var received = new ConcurrentQueue<DiagnosticsReceivedEventArgs>();
        controller.DiagnosticsReceived += (_, e) => received.Enqueue(e);
        artNet.AddInstance(controller);

        Assert.That(await within(async () => await node.SendDiagnostics("waiting for the poll", EPriorityCode.DpHigh)), Is.True,
            "The controller's ArtPoll registered it for diagnostics");
        Assert.That(await within(() => received.Any(e => e.Diagnostics.Text == "waiting for the poll")), Is.True);

        Assert.That(await node.SendDiagnostics("too unimportant", EPriorityCode.DpLow), Is.False, "Below the requested minimum priority");
        Assert.That(await node.SendDiagnostics("critical", EPriorityCode.DpCritical, 1), Is.True);
        Assert.That(await within(() => received.Any(e => e.Diagnostics.Text == "critical" && e.Diagnostics.LogicalPort == 1)), Is.True);
    }

    [Test]
    public async Task SeveralControllersUseTheLowestMinimumPriority()
    {
        pollFrom(new IPv4Address("10.0.0.2"), EArtPollFlags.DiagnosticEnabled | EArtPollFlags.DiagnosticUnicast, EPriorityCode.DpHigh);
        Assert.That(await node.SendDiagnostics("medium", EPriorityCode.DpMed), Is.False, "Only one controller, it wants DpHigh and up");

        pollFrom(new IPv4Address("10.0.0.3"), EArtPollFlags.DiagnosticEnabled, EPriorityCode.DpMed);
        Assert.That(await node.SendDiagnostics("medium", EPriorityCode.DpMed), Is.True, "Lowest minimum of all controllers applies");
        Assert.That(await node.SendDiagnostics("low", EPriorityCode.DpLow), Is.False);
    }

    [Test]
    public async Task PollWithoutTheFlagEndsTheRequest()
    {
        var controllerIp = new IPv4Address("10.0.0.2");
        pollFrom(controllerIp, EArtPollFlags.DiagnosticEnabled, EPriorityCode.DpLow);
        Assert.That(await node.SendDiagnostics("hello"), Is.True);

        pollFrom(controllerIp, EArtPollFlags.None);
        Assert.That(await node.SendDiagnostics("hello again"), Is.False);
    }

    private static async Task<bool> within(Func<Task<bool>> condition, int timeoutMs = 8000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (await condition())
                return true;
            await Task.Delay(50);
        }
        return false;
    }
}
