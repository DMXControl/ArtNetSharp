using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks.Instances;

namespace ArtNetTests;

/// <summary>
/// Receive-side merging (Spec: Data Merging): a source is IP + Physical, at most two sources are merged,
/// a failed source is held for 10 s, if all fail the last merge result is held, GoodOutput bit 3 while merging.
/// Packets are handed directly to the node; the 10 s timeout is shortened for the test.
/// </summary>
[NonParallelizable]
public class DmxMergeTest
{
    private static readonly PortAddress portAddress = new PortAddress(0, 0, 5);
    private static readonly IPv4Address localIp = new IPv4Address("10.0.0.1");
    private static readonly IPv4Address ipA = new IPv4Address("10.0.0.2");
    private static readonly IPv4Address ipB = new IPv4Address("10.0.0.3");
    private static readonly IPv4Address ipC = new IPv4Address("10.0.0.4");
    private static readonly TimeSpan timeout = TimeSpan.FromMilliseconds(400);
    private TimeSpan originalTimeout;
    private ArtNet artNet;
    private NodeMock node;

    [SetUp]
    public void SetUp()
    {
        originalTimeout = AbstractInstance.MergeSourceTimeout;
        AbstractInstance.MergeSourceTimeout = timeout;
        artNet = new ArtNet();
        node = new NodeMock(artNet) { Name = "Merge Test Node" };
        node.AddPortConfig(new PortConfig(1, portAddress, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet });
        artNet.AddInstance(node);
    }

    [TearDown]
    public void TearDown()
    {
        ((IDisposable)artNet)?.Dispose();
        AbstractInstance.MergeSourceTimeout = originalTimeout;
    }

    /// <summary>Sends channel 1 = <paramref name="ch1"/>, channel 2 = <paramref name="ch2"/> from the given source (Sequence 0 = always accepted).</summary>
    private void send(IPv4Address source, byte ch1, byte ch2, byte physical = 1)
    {
        byte[] data = new byte[512];
        data[0] = ch1;
        data[1] = ch2;
        using var artDmx = new ArtDMX(0, physical, portAddress.Net, portAddress.Address, data);
        ((IInstance)node).PacketReceived(artDmx, localIp, source);
    }

    private byte[] received(EMergeMode mode = EMergeMode.HTP) => node.GetReceivedDMX(portAddress, mode)!.Take(2).ToArray();
    private bool merging => node.PortConfigs[0].GoodOutput.MergingArtNetData;

    [Test]
    public void SingleSourceIsNotMerging()
    {
        send(ipA, 10, 20);
        Assert.That(received(), Is.EqualTo(new byte[] { 10, 20 }));
        Assert.That(merging, Is.False);
    }

    [Test]
    public void TwoSourcesAreMergedHtpAndFlagged()
    {
        send(ipA, 100, 10);
        send(ipB, 50, 200);
        Assert.That(received(), Is.EqualTo(new byte[] { 100, 200 }));
        Assert.That(merging, Is.True, "GoodOutput bit 3 must be set while merging");
    }

    [Test]
    public void SameIpDifferentPhysicalAreTwoSources()
    {
        send(ipA, 100, 10, physical: 1);
        send(ipA, 50, 200, physical: 2);
        Assert.That(received(), Is.EqualTo(new byte[] { 100, 200 }));
        Assert.That(merging, Is.True);
    }

    [Test]
    public void ThirdSourceIsIgnored()
    {
        send(ipA, 10, 10);
        send(ipB, 20, 20);
        send(ipC, 255, 255);
        Assert.That(received(), Is.EqualTo(new byte[] { 20, 20 }), "Merging is limited to two sources");
    }

    [Test]
    public async Task FailedSourceLeavesMergeAfterTimeout()
    {
        send(ipA, 200, 200);
        send(ipB, 50, 50);
        Assert.That(received(), Is.EqualTo(new byte[] { 200, 200 }));

        // A fails, B keeps sending
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout + TimeSpan.FromMilliseconds(200))
        {
            send(ipB, 50, 50);
            await Task.Delay(20);
        }

        Assert.That(received(), Is.EqualTo(new byte[] { 50, 50 }), "Stale values of the failed source must leave the merge");
        Assert.That(merging, Is.False, "Merge mode ends when the failed source does not recover");

        // The freed slot can be taken by a new source
        send(ipC, 60, 70);
        Assert.That(received(), Is.EqualTo(new byte[] { 60, 70 }));
        Assert.That(merging, Is.True);
    }

    [Test]
    public async Task BothSourcesFailHoldsLastMergeResult()
    {
        send(ipA, 200, 10);
        send(ipB, 50, 100);
        await Task.Delay(timeout + TimeSpan.FromMilliseconds(200));
        Assert.That(received(), Is.EqualTo(new byte[] { 200, 100 }), "If both sources fail, the output holds the last merge result");
    }

    [Test]
    public async Task OneFailsThenTheOtherHoldsTheLastResult()
    {
        send(ipA, 200, 10);
        send(ipB, 50, 100);
        // A fails, B continues past the timeout, then B fails as well
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout + TimeSpan.FromMilliseconds(200))
        {
            send(ipB, 50, 100);
            await Task.Delay(20);
        }
        await Task.Delay(timeout + TimeSpan.FromMilliseconds(200));

        Assert.That(received(), Is.EqualTo(new byte[] { 50, 100 }), "Last merge result was B alone, A must not come back");
    }

    [Test]
    public void LtpTakesTheLatestSource()
    {
        send(ipA, 200, 200);
        send(ipB, 10, 20);
        Assert.That(received(EMergeMode.LTP), Is.EqualTo(new byte[] { 10, 20 }));
    }
}
