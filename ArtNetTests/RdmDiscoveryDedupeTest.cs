using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Binary_Tests;
using ArtNetTests.Binary_Tests.Luminex;
using ArtNetTests.Mocks;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ArtNetTests;

/// <summary>
/// LumiNode12 FW 2.9.1: output 1 (BindIndex 1) and the process blocks without a physical port (BindIndex 13-16)
/// all report Universe 0. PerformRDMDiscovery sent one ArtTodRequest / AtcFlush per remote port, so 5 identical
/// packets went to the node (seen on the device). One per IP and Port-Address is enough.
/// Real ArtPollReplies of the device; sending goes through the loop adapter.
/// </summary>
[NonParallelizable]
public class RdmDiscoveryDedupeTest
{
    private static readonly IPv4Address nodeIp = new IPv4Address("2.164.93.125");
    private static readonly IPv4Address localIp = new IPv4Address("2.168.22.44");
    private static readonly PortAddress universe0 = new PortAddress(0, 0, 0);
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
        controller = new ControllerInstanceMock(artNet, 0x4258) { Name = "Discovery Dedupe Controller" };
        artNet.AddInstance(controller);

        var bindIndex13 = new Luminex_LumiNode12_2_9_1_BindIndex13().Data;
        foreach (var data in new[] { new Luminex_LumiNode12_2_9_1_BindIndex1().Data, bindIndex13, withBindIndex(bindIndex13, 14), withBindIndex(bindIndex13, 15), withBindIndex(bindIndex13, 16) })
            using (var reply = new ArtPollReply(data))
                ((IInstance)controller).PacketReceived(reply, localIp, nodeIp);

        Assert.That(await within(() => controller.RemoteClients.FirstOrDefault()?.Ports.Count(p => p.OutputPortAddress == universe0 && p.IsRDMCapable) == 5), Is.True,
            "5 RDM capable ports on Universe 0");
        await Task.Delay(100);
        sent.Clear();
    }

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    private static byte[] withBindIndex(byte[] data, byte bindIndex)
    {
        byte[] copy = (byte[])data.Clone();
        copy[211] = bindIndex;
        return copy;
    }

    private static async Task<bool> within(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(10);
        return condition();
    }

    private List<T> sentPackets<T>() where T : AbstractArtPacketCore
        => sent.Select(d => ArtNetSharp.Tools.TryDeserializePacket(d, out var p) ? p : null).OfType<T>().ToList();

    [Test]
    public async Task OneTodRequestPerAddress()
    {
        await controller.PerformRDMDiscovery(universe0);
        await Task.Delay(100);

        var requests = sentPackets<ArtTodRequest>();
        Assert.That(requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task OneAtcFlushPerAddress()
    {
        await controller.PerformRDMDiscovery(universe0, flush: true);
        await Task.Delay(100);

        Assert.That(sentPackets<ArtTodControl>(), Has.Count.EqualTo(1));
    }
}
