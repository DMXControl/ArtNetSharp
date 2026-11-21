using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks.Instances;
using System.Diagnostics;

namespace ArtNetTests;

/// <summary>
/// ArtPoll Flags bit 1: "Send ArtPollReply whenever Node conditions change". After such a poll the node sends an
/// unsolicited ArtPollReply when e.g. its name or a port changes; without the flag only on ArtPoll/ArtAddress.
/// Packets travel over the loop adapter; neither instance polls on its own (no input ports).
/// </summary>
public class ReplyOnChangeTest
{
    private static readonly IPv4Address loopIp = new IPv4Address("3.3.3.3"); // address used by the loop adapter
    private ArtNet artNet;
    private NodeMock node;
    private ControllerInstance controller;

    [SetUp]
    public void SetUp()
    {
        artNet = new ArtNet();
        artNet.LoopNetwork = new ArtNet.NetworkLoopAdapter(new IPv4Address("255.0.0.0"));
        // The loop adapter replaces sending only; ignore real traffic (e.g. ArtPolls from other software on this machine).
        foreach (var nic in artNet.NetworkClients)
            nic.Enabled = false;
        node = new NodeMock(artNet) { Name = "Change Node", ShortName = "Change" };
        node.AddPortConfig(new PortConfig(1, new PortAddress(0, 0, 1), true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet });
        controller = new ControllerInstance(artNet) { Name = "Change Controller" };
        artNet.AddInstance(node, controller);
    }

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    private void poll(EArtPollFlags flags)
    {
        using var artPoll = new ArtPoll(0x4249, 0x0001, flags: flags);
        ((IInstance)node).PacketReceived(artPoll, loopIp, loopIp);
    }

    private bool controllerSees(string longName) => controller.RemoteClients?.Any(rc => rc.LongName == longName) == true;

    private static async Task<bool> within(TimeSpan time, Func<bool> condition)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < time)
        {
            if (condition())
                return true;
            await Task.Delay(10);
        }
        return condition();
    }

    [Test]
    public async Task ChangeIsReportedWithoutPolling()
    {
        poll(EArtPollFlags.ArtPollReplyOnChange);
        Assert.That(await within(TimeSpan.FromSeconds(3), () => controllerSees("Change Node")), Is.True, "normal reply to the poll");

        node.Name = "Renamed Node";

        Assert.That(await within(TimeSpan.FromSeconds(3), () => controllerSees("Renamed Node")), Is.True,
            "The change must be reported by itself, without another ArtPoll");
    }

    [Test]
    public async Task LocateIsReportedToTheController()
    {
        poll(EArtPollFlags.ArtPollReplyOnChange);
        Assert.That(await within(TimeSpan.FromSeconds(3), () => controllerSees("Change Node")), Is.True);

        using (var locate = ArtAddress.CreateSetCommand(1, new ArtAddressCommand(EArtAddressCommand.LedLocate)))
            ((IInstance)node).PacketReceived(locate, loopIp, loopIp);

        Assert.That(await within(TimeSpan.FromSeconds(3), () =>
            controller.RemoteClients.FirstOrDefault(rc => rc.LongName == "Change Node")?.Root?.Status.IndicatorState == NodeStatus.EIndicatorState.Locate),
            Is.True, "Status1 of the ArtPollReply shows the Locate state");
    }

    [Test]
    public async Task WithoutTheFlagNothingIsSent()
    {
        poll(EArtPollFlags.None);
        Assert.That(await within(TimeSpan.FromSeconds(3), () => controllerSees("Change Node")), Is.True, "normal reply to the poll");

        node.Name = "Renamed Node";

        Assert.That(await within(TimeSpan.FromSeconds(1), () => controllerSees("Renamed Node")), Is.False,
            "Without the flag the node only replies to ArtPoll/ArtAddress");
    }

    [Test]
    public async Task PollWithoutTheFlagEndsTheSubscription()
    {
        poll(EArtPollFlags.ArtPollReplyOnChange);
        Assert.That(await within(TimeSpan.FromSeconds(3), () => controllerSees("Change Node")), Is.True);
        poll(EArtPollFlags.None); // the controller switches it off again
        await Task.Delay(2000); // let the reply to this poll (random delay up to 800 ms) arrive, also under load

        node.Name = "Renamed Node";

        Assert.That(await within(TimeSpan.FromSeconds(1), () => controllerSees("Renamed Node")), Is.False);
    }
}
