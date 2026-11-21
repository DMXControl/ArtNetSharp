using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using ArtNetTests.Mocks.Instances;
using System.Net;

namespace ArtNetTests;

/// <summary>
/// End-to-end test of the controller's DMX send loop over loopback:
/// data written on the controller must arrive at the subscribed node,
/// and ArtSync must be sent at most once per frame (max. 44 Hz), also with several universes.
/// </summary>
[DependsOnFixture(typeof(BroadcastReceiveTest))]
public class DmxSendTest
{
    private const int universes = 8;
    private ArtNet artNet;
    private NodeMock node;
    private ControllerInstanceMock controller;
    private static readonly PortAddress[] portAddresses = Enumerable.Range(1, universes).Select(u => new PortAddress(0, 0, (byte)u)).ToArray();
    /// <summary>Own universe for <see cref="FirstWriteHonoursDestinationIndexAndCount"/>, so it is written there for the first time.</summary>
    private static readonly PortAddress partialPortAddress = new PortAddress(0, 0, universes + 1);

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        artNet = new ArtNet();
        // Only loopback, so the test does not depend on (or disturb) the real network.
        foreach (var nic in artNet.NetworkClients)
            if (!IPAddress.IsLoopback(nic.LocalIpAddress))
                nic.Enabled = false;

        node = new NodeMock(artNet) { Name = "DMX Test Node" };
        controller = new ControllerInstanceMock(artNet, 0x4244) { Name = "DMX Test Controller" };
        for (byte i = 0; i < universes; i++)
        {
            byte bindIndex = (byte)(i + 1);
            node.AddPortConfig(new PortConfig(bindIndex, portAddresses[i], true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet, GoodOutput = new GoodOutput(outputStyle: GoodOutput.EOutputStyle.Continuous, isBeingOutputAsDMX: true) });
            controller.AddPortConfig(new PortConfig(bindIndex, portAddresses[i], false, true) { PortNumber = 1, Type = EPortType.InputToArtNet | EPortType.ArtNet });
        }
        node.AddPortConfig(new PortConfig(universes + 1, partialPortAddress, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet, GoodOutput = new GoodOutput(outputStyle: GoodOutput.EOutputStyle.Continuous, isBeingOutputAsDMX: true) });
        controller.AddPortConfig(new PortConfig(universes + 1, partialPortAddress, false, true) { PortNumber = 1, Type = EPortType.InputToArtNet | EPortType.ArtNet });
        artNet.AddInstance(node, controller);

        for (int i = 0; i < 300; i++)
        {
            if (portAddresses.Append(partialPortAddress).All(pa => controller.RemoteClientsPorts?.Any(p => p.OutputPortAddress == pa) == true))
                return;
            await Task.Delay(100);
        }
        Assert.Fail("Node ports were not discovered by the controller");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => ((IDisposable)artNet)?.Dispose();

    [Test, CancelAfter(20_000)]
    public async Task FirstWriteHonoursDestinationIndexAndCount(CancellationToken cancellationToken)
    {
        // Very first write to this universe: 10 values to channels 101..110 (index 100).
        byte[] values = Enumerable.Range(1, 10).Select(v => (byte)v).ToArray();
        controller.WriteDMXValues(partialPortAddress, values, destinationIndex: 100, count: 10);
        // count without destinationIndex: only the first 3 of 20 values go to channels 1..3.
        byte[] twenty = Enumerable.Repeat((byte)0xAA, 20).ToArray();
        controller.WriteDMXValues(partialPortAddress, twenty, count: 3);

        byte[]? received;
        while (true)
        {
            received = node.GetReceivedDMX(partialPortAddress);
            if (received != null && received[100] == 1 && received[0] == 0xAA)
                break;
            await Task.Delay(20, cancellationToken); // throws (= fails) on CancelAfter if the data never arrives
        }

        Assert.Multiple(() =>
        {
            Assert.That(received.Skip(100).Take(10), Is.EqualTo(values), "Channels 101..110");
            Assert.That(received.Take(3), Is.All.EqualTo(0xAA), "Channels 1..3");
            Assert.That(received.Skip(3).Take(97), Is.All.EqualTo(0), "Channels 4..100 must stay untouched");
            Assert.That(received.Skip(110), Is.All.EqualTo(0), "Channels 111..512 must stay untouched");
        });
    }

    [Test, CancelAfter(20_000)]
    public async Task DataArrivesAndArtSyncIsRateLimited(CancellationToken cancellationToken)
    {
        int syncCount = 0;
        node.SyncReceived += (_, _) => Interlocked.Increment(ref syncCount);

        // Change the universes one after another every few ms for 2s: far faster than 44Hz and
        // never all at once, so the send loop has to group them into frames and rate-limit.
        byte[][] data = portAddresses.Select(_ => new byte[512]).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        byte value = 0;
        int u = 0;
        while (sw.ElapsedMilliseconds < 2000)
        {
            u = (u + 1) % universes;
            data[u][0] = ++value;
            data[u][511] = (byte)(255 - value);
            controller.WriteDMXValues(portAddresses[u], data[u]);
            await Task.Delay(3, cancellationToken);
        }
        double seconds = sw.Elapsed.TotalSeconds;
        int syncs = Volatile.Read(ref syncCount);

        // The last written frame of every universe must arrive at the node.
        for (int i = 0; i < universes; i++)
        {
            while (true)
            {
                byte[]? received = node.GetReceivedDMX(portAddresses[i]);
                if (received != null && received[0] == data[i][0] && received[511] == data[i][511])
                    break;
                await Task.Delay(20, cancellationToken); // throws (= fails) on CancelAfter if it never arrives
            }
        }

        Console.WriteLine($"ArtSync received: {syncs} in {seconds:0.00}s ({syncs / seconds:0.0}/s)");
        Assert.That(syncs, Is.GreaterThan(0), "No ArtSync was sent");
        // At most one ArtSync per frame, frames limited to 44Hz (+ small tolerance for timer jitter).
        Assert.That(syncs / seconds, Is.LessThanOrEqualTo(48));
    }
}
