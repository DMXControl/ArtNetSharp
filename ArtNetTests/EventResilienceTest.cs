using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks.Instances;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;
using System.Diagnostics;

namespace ArtNetTests;

/// <summary>
/// Application event handlers must neither block nor break the library: events are raised ordered and
/// fail-safe on the instance's event queue, not on the receive loop. Packets are handed directly to the node.
/// </summary>
public class EventResilienceTest
{
    private static readonly PortAddress portAddress = new PortAddress(0, 0, 6);
    private static readonly IPv4Address localIp = new IPv4Address("10.0.0.1");
    private static readonly IPv4Address sourceIp = new IPv4Address("10.0.0.2");
    private ArtNet artNet;
    private NodeMock node;

    [SetUp]
    public void SetUp()
    {
        artNet = new ArtNet();
        node = new NodeMock(artNet) { Name = "Event Test Node" };
        node.AddPortConfig(new PortConfig(1, portAddress, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet });
        artNet.AddInstance(node);
    }

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    private void receive(AbstractArtPacketCore packet)
    {
        using (packet)
            ((IInstance)node).PacketReceived(packet, localIp, sourceIp);
    }

    private void receiveDmx(byte value)
    {
        byte[] data = new byte[512];
        data[0] = value;
        receive(new ArtDMX(0, 1, portAddress.Net, portAddress.Address, data));
    }

    private static async Task waitFor(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(10);
    }

    [Test]
    public async Task BlockingDmxHandlerDoesNotBlockReception()
    {
        using var gate = new ManualResetEventSlim(false);
        int calls = 0;
        byte lastSeenByHandler = 0;
        node.DMXReceived += (_, pa) =>
        {
            Interlocked.Increment(ref calls);
            gate.Wait(); // application handler blocks (e.g. Dispatcher.Invoke on a busy UI thread)
            lastSeenByHandler = node.GetReceivedDMX(pa)![0];
        };

        var sw = Stopwatch.StartNew();
        for (int i = 1; i <= 250; i++)
            receiveDmx((byte)i);
        sw.Stop();

        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000), "Receiving must not wait for the blocked handler");
        Assert.That(node.GetReceivedDMX(portAddress)![0], Is.EqualTo(250), "Data is processed although the handler blocks");

        gate.Set();
        await waitFor(() => lastSeenByHandler == 250);

        Assert.That(lastSeenByHandler, Is.EqualTo(250), "After unblocking, the handler sees the latest data");
        Assert.That(Volatile.Read(ref calls), Is.LessThanOrEqualTo(3), "DMXReceived is coalesced per port instead of queuing 250 events");
    }

    [Test]
    public async Task ThrowingHandlerDoesNotBreakOtherHandlersOrTheLibrary()
    {
        int healthy = 0;
        node.SyncReceived += (_, _) => throw new InvalidOperationException("application bug");
        node.SyncReceived += (_, _) => Interlocked.Increment(ref healthy);
        node.DMXReceived += (_, _) => throw new InvalidOperationException("application bug");

        for (int i = 0; i < 3; i++)
            receive(new ArtSync());
        receiveDmx(42);

        await waitFor(() => Volatile.Read(ref healthy) == 3);
        Assert.That(Volatile.Read(ref healthy), Is.EqualTo(3), "The other handler still gets every event");
        Assert.That(node.GetReceivedDMX(portAddress)![0], Is.EqualTo(42), "The library keeps working");
    }

    [Test]
    public async Task EventsKeepTheirOrder()
    {
        var frames = new List<byte>();
        node.TimeCodeReceived += (_, tc) => { lock (frames) frames.Add(tc.Frames); };

        for (byte f = 0; f < 25; f++)
            receive(new ArtTimeCode(0, f, 0, 0, 0, ETimecodeType.EBU));

        await waitFor(() => { lock (frames) return frames.Count == 25; });
        Assert.That(frames, Is.EqualTo(Enumerable.Range(0, 25).Select(f => (byte)f)));
    }

    [Test]
    public async Task BlockingRdmRequestHandlerDoesNotBlockReception()
    {
        using var gate = new ManualResetEventSlim(false);
        int handled = 0;
        node.RequestRDMMessageReceived += (_, _) =>
        {
            gate.Wait();
            Interlocked.Increment(ref handled);
        };

        var message = new RDMMessage()
        {
            SourceUID = new UID(0x1122, 0x33445566),
            DestUID = new UID(0x3344, 0x55667788),
            Command = ERDM_Command.GET_COMMAND,
            Parameter = ERDM_Parameter.DEVICE_INFO,
            TransactionCounter = 1,
        };
        using var created = new ArtRDM(new PortAddress(0, 0, 1), message);

        var sw = Stopwatch.StartNew();
        receive(new ArtRDM(created.GetPacket()));
        receiveDmx(7);
        sw.Stop();

        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500), "The RDM request handler must not run on the receive loop");
        Assert.That(node.GetReceivedDMX(portAddress)![0], Is.EqualTo(7));

        gate.Set();
        await waitFor(() => Volatile.Read(ref handled) == 1);
        Assert.That(Volatile.Read(ref handled), Is.EqualTo(1));
    }
}
