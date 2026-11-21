using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks.Instances;

namespace ArtNetTests;

/// <summary>
/// Receive side of the ArtDmx Sequence field (Spec: 0x01..0xff re-sequences, 0x00 disables).
/// Packets are handed directly to the node, so the test is deterministic and needs no network.
/// </summary>
public class DmxSequenceTest
{
    private static readonly PortAddress portAddress = new PortAddress(0, 0, 3);
    private static readonly IPv4Address localIp = new IPv4Address("10.0.0.1");
    private static readonly IPv4Address sourceIp = new IPv4Address("10.0.0.2");
    private ArtNet artNet;
    private NodeMock node;

    [SetUp]
    public void SetUp()
    {
        artNet = new ArtNet();
        node = new NodeMock(artNet) { Name = "Sequence Test Node" };
        node.AddPortConfig(new PortConfig(1, portAddress, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet });
        artNet.AddInstance(node);
    }

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    /// <summary>Sends one ArtDmx whose channel 1 carries <paramref name="value"/>, returns the node's channel 1 afterwards.</summary>
    private byte send(byte sequence, byte value)
    {
        byte[] data = new byte[512];
        data[0] = value;
        using var artDmx = new ArtDMX(sequence, 1, portAddress.Net, portAddress.Address, data);
        ((IInstance)node).PacketReceived(artDmx, localIp, sourceIp);
        return node.GetReceivedDMX(portAddress)![0];
    }

    [Test]
    public void InOrderPacketsAreAccepted()
    {
        for (byte s = 1; s <= 50; s++)
            Assert.That(send(s, s), Is.EqualTo(s));
    }

    [Test]
    public void SingleOutOfOrderPacketIsDropped()
    {
        send(10, 10);
        send(11, 11);
        Assert.That(send(9, 99), Is.EqualTo(11), "An older packet arriving late must not overwrite newer data");
        Assert.That(send(12, 12), Is.EqualTo(12));
    }

    [Test]
    public void WrapAroundIsAccepted()
    {
        send(254, 1);
        send(255, 2);
        Assert.That(send(1, 3), Is.EqualTo(3), "0xff -> 0x01 is the normal wrap (0x00 is skipped)");
    }

    [Test]
    public void WrapAroundWithBigStepIsAccepted()
    {
        // Steps of 3 used to be dropped at the wrap (254 -> 1) until the counter passed 254 again (~2s freeze).
        send(251, 1);
        send(254, 2);
        Assert.That(send(1, 3), Is.EqualTo(3));
        Assert.That(send(4, 4), Is.EqualTo(4));
    }

    [Test]
    public void SequenceZeroIsAlwaysAccepted()
    {
        send(100, 1);
        Assert.That(send(0, 2), Is.EqualTo(2), "0x00 disables sequencing for this packet");
    }

    [Test]
    public void OlderPacketFarBehindIsDropped()
    {
        send(199, 199);
        send(200, 200);
        Assert.That(send(150, 150), Is.EqualTo(200), "A packet 50 steps behind is old, not a wrap-around");
    }

    [Test]
    public void RestartedSourceIsResyncedQuickly()
    {
        // Source ran up to 100 and restarts at 1 shortly after (< SequenceResetTime).
        for (byte s = 50; s <= 100; s++)
            send(s, 1);

        byte value = 10;
        byte accepted = 0;
        for (byte s = 1; s <= 20; s++)
        {
            if (send(s, ++value) == value)
            {
                accepted = s;
                break;
            }
        }

        Assert.That(accepted, Is.Not.EqualTo(0), "Restarted source was not accepted within 20 frames (output frozen)");
        Assert.That(accepted, Is.LessThanOrEqualTo(4), "Resync must happen after a few packets, not after ~100 frames");
    }

    [Test]
    public async Task SequenceIsResetAfterPause()
    {
        send(100, 1);
        await Task.Delay(TimeSpan.FromSeconds(1.1));
        Assert.That(send(1, 2), Is.EqualTo(2), "After a pause the first packet must be accepted regardless of its sequence");
    }
}
