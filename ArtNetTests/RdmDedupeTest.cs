using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks.Instances;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;

namespace ArtNetTests;

/// <summary>An ArtRDM request with the same transaction is handled once within the 2 s duplicate window.</summary>
public class RdmDedupeTest
{
    private static readonly IPv4Address localIp = new IPv4Address("10.0.0.1");
    private static readonly IPv4Address sourceIp = new IPv4Address("10.0.0.2");
    private ArtNet artNet;
    private NodeMock node;

    [SetUp]
    public void SetUp()
    {
        artNet = new ArtNet();
        node = new NodeMock(artNet) { Name = "RDM Dedupe Node" };
        artNet.AddInstance(node);
    }

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    private static ArtRDM request(byte transaction)
    {
        var message = new RDMMessage()
        {
            SourceUID = new UID(0x1122, 0x33445566),
            DestUID = new UID(0x3344, 0x55667788),
            Command = ERDM_Command.GET_COMMAND,
            Parameter = ERDM_Parameter.DEVICE_INFO,
            TransactionCounter = transaction,
        };
        using var created = new ArtRDM(new PortAddress(0, 0, 1), message);
        return new ArtRDM(created.GetPacket()); // round trip, like a packet from the network (valid checksum)
    }

    [Test]
    public async Task DuplicateWithinWindowIsHandledOnce()
    {
        int handled = 0;
        node.RequestRDMMessageReceived += (_, _) => Interlocked.Increment(ref handled);

        using (var first = request(7))
        {
            Assert.That(first.RDMMessage.ChecksumValid, Is.True, "test setup: checksum must be valid");
            ((IInstance)node).PacketReceived(first, localIp, sourceIp);
        }
        using (var duplicate = request(7))
            ((IInstance)node).PacketReceived(duplicate, localIp, sourceIp);
        using (var other = request(8))
            ((IInstance)node).PacketReceived(other, localIp, sourceIp);

        await Task.Delay(200);
        Assert.That(Volatile.Read(ref handled), Is.EqualTo(2), "Transaction 7 once (duplicate dropped) + transaction 8");

        // After the window the same transaction number counts as a new request again.
        await Task.Delay(TimeSpan.FromSeconds(2.1));
        using (var again = request(7))
            ((IInstance)node).PacketReceived(again, localIp, sourceIp);
        await Task.Delay(200);
        Assert.That(Volatile.Read(ref handled), Is.EqualTo(3));
    }
}
