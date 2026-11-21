using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Binary_Tests;
using ArtNetTests.Binary_Tests.Botex;
using ArtNetTests.Mocks;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ArtNetTests;

/// <summary>
/// A ToD belongs to one output (BindIndex + Port-Address). The Botex NETcon8-3 announces 4 ports per ArtPollReply
/// (BindIndex 1: outputs 1-4, BindIndex 2: outputs 5-8). The ToD of output 1 was stored on all 4 ports of
/// BindIndex 1, so every ArtRdm to one of its responders was sent 4 times (seen in a Wireshark dump and on the device).
/// Real ArtPollReplies of the device, received by a controller; sending goes through the loop adapter.
/// </summary>
[NonParallelizable]
public class TodPortAssignmentTest
{
    private static readonly IPv4Address botexIp = new IPv4Address("2.168.15.11");
    private static readonly IPv4Address localIp = new IPv4Address("2.168.22.44");
    private static readonly PortAddress output1 = new PortAddress(0, 0, 1);
    private static readonly UID[] responders = [new UID(0x4C4C, 0x4000068F), new UID(0x4C55, 0x0000D5AA), new UID(0x5753, 0x6E923091)];
    private ArtNet artNet;
    private ControllerInstanceMock controller;
    private readonly ConcurrentQueue<byte[]> sent = new();

    [SetUp]
    public async Task SetUp()
    {
        artNet = new ArtNet();
        var loop = new ArtNet.NetworkLoopAdapter(new IPv4Address("255.0.0.0"));
        loop.Sent += data => sent.Enqueue(data);
        artNet.LoopNetwork = loop;
        foreach (var nic in artNet.NetworkClients)
            nic.Enabled = false;
        controller = new ControllerInstanceMock(artNet, 0x4257) { Name = "ToD Port Controller" };
        artNet.AddInstance(controller);

        foreach (var subject in new AbstractArtPollReplyBinaryTestSubject[] { new Botex_NETcon8_3_2_6_7_BindIndex1(), new Botex_NETcon8_3_2_6_7_BindIndex2() })
            using (var reply = new ArtPollReply(subject.Data))
                ((IInstance)controller).PacketReceived(reply, localIp, botexIp);

        Assert.That(await within(() => controller.RemoteClients.FirstOrDefault()?.Ports.Count == 8), Is.True, "2 ArtPollReplies x 4 ports");
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

    private IReadOnlyCollection<RemoteClientPort> ports => controller.RemoteClients.First().Ports;

    private void receiveTod(PortAddress portAddress, byte port, byte bindIndex, params UID[] uids)
    {
        using var tod = new ArtTodData(portAddress, port, bindIndex, (ushort)uids.Length, 0, uids, EArtTodDataCommandResponse.TodFull);
        ((IInstance)controller).PacketReceived(tod, localIp, botexIp);
    }

    [Test]
    public async Task TodLandsOnlyOnItsOutput()
    {
        receiveTod(output1, 1, 1, responders);
        Assert.That(await within(() => ports.Any(p => p.KnownResponderRDMUIDs.Count != 0)), Is.True);

        var withUids = ports.Where(p => p.KnownResponderRDMUIDs.Count != 0).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(withUids, Has.Count.EqualTo(1));
            Assert.That(withUids[0].BindIndex, Is.EqualTo(1));
            Assert.That(withUids[0].OutputPortAddress, Is.EqualTo(output1));
            Assert.That(withUids[0].KnownResponderRDMUIDs.Select(b => b.Uid), Is.EquivalentTo(responders));
        });
    }

    [Test]
    public async Task EmptyTodOfAnotherOutputKeepsTheResponders()
    {
        receiveTod(output1, 1, 1, responders);
        Assert.That(await within(() => ports.Any(p => p.KnownResponderRDMUIDs.Count != 0)), Is.True);

        // the Botex answers every ArtTodRequest; outputs 2 and 5 have no responders
        receiveTod(new PortAddress(0, 0, 2), 2, 1);
        receiveTod(new PortAddress(0, 0, 5), 1, 2);
        await Task.Delay(200);

        Assert.That(ports.Single(p => p.OutputPortAddress == output1).KnownResponderRDMUIDs, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ArtRdmIsSentOnce()
    {
        receiveTod(output1, 1, 1, responders);
        Assert.That(await within(() => ports.Any(p => p.KnownResponderRDMUIDs.Count != 0)), Is.True);
        await Task.Delay(100);
        sent.Clear();

        await controller.SendArtRDM(new RDMMessage
        {
            Command = ERDM_Command.GET_COMMAND,
            Parameter = ERDM_Parameter.DEVICE_INFO,
            DestUID = responders[0],
            TransactionCounter = 42,
        });
        await Task.Delay(200);

        var artRdms = sent.Where(d => ArtNetSharp.Tools.TryDeserializePacket(d, out var p) && p is ArtRDM).ToList();
        Assert.That(artRdms, Has.Count.EqualTo(1), "One ArtRdm per request");
        Assert.That(ArtNetSharp.Tools.TryDeserializePacket(artRdms[0], out var packet), Is.True);
        Assert.That(((ArtRDM)packet).PortAddress, Is.EqualTo(output1));
    }
}
