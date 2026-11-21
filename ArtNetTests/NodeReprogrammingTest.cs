using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetSharp.Misc;
using ArtNetTests.Mocks;
using ArtNetTests.Mocks.Instances;
using System.Diagnostics;

namespace ArtNetTests;

/// <summary>
/// ArtAddress (remote reprogramming of a node): names, Net/Sub-Net/SwIn/SwOut incl. reset, merge mode and
/// AcCancelMerge, the veto event, and that controllers ignore it. Packets are handed directly to the instance.
/// </summary>
[NonParallelizable]
public class NodeReprogrammingTest
{
    private static readonly IPv4Address localIp = new IPv4Address("10.0.0.1");
    private static readonly IPv4Address controllerIp = new IPv4Address("10.0.0.2");
    private static readonly IPv4Address ipA = new IPv4Address("10.0.0.3");
    private static readonly IPv4Address ipB = new IPv4Address("10.0.0.4");
    private static readonly PortAddress initial1 = new PortAddress(0, 0, 3);
    private static readonly PortAddress initial2 = new PortAddress(0, 0, 4);
    private ArtNet artNet;
    private NodeMock node;
    private PortConfig port1;
    private PortConfig port2;

    [SetUp]
    public void SetUp()
    {
        artNet = new ArtNet();
        node = new NodeMock(artNet) { Name = "Original Long", ShortName = "Original" };
        // Two ports on the same BindIndex, so the port index inside SwOut[] matters.
        port1 = new PortConfig(1, initial1, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet };
        port2 = new PortConfig(1, initial2, true, false) { PortNumber = 2, Type = EPortType.OutputFromArtNet };
        node.AddPortConfig(port1, port2);
        artNet.AddInstance(node);
    }

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    private void receive(AbstractInstance instance, AbstractArtPacketCore packet, IPv4Address? source = null)
    {
        using (packet)
            ((IInstance)instance).PacketReceived(packet, localIp, source ?? controllerIp);
    }

    /// <summary>Round trip through bytes, optionally patched (e.g. to send the "reset" value 0x00).</summary>
    private static ArtAddress fromBytes(ArtAddress created, Action<byte[]>? patch = null)
    {
        byte[] data;
        using (created)
            data = (byte[])created.GetPacket().Clone();
        patch?.Invoke(data);
        return new ArtAddress(data);
    }

    private static async Task waitFor(Func<bool> condition)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < 3000)
            await Task.Delay(10);
    }

    [Test]
    public async Task NamesAndPortAddressAreProgrammed()
    {
        receive(node, fromBytes(new ArtAddress(1, (byte)0x45, (byte)7, new Universe?[] { null, (byte)9 }, null, "New Short", "New Long Name", null, null)));
        await waitFor(() => node.Name == "New Long Name" && port2.PortAddress == new PortAddress(0x45, 7, 9));

        Assert.Multiple(() =>
        {
            Assert.That(node.ShortName, Is.EqualTo("New Short"));
            Assert.That(node.Name, Is.EqualTo("New Long Name"));
            // Net is 7 bit (0x45 = 69, was masked to 4 bit before); Net/Sub-Net apply to all ports of the BindIndex
            Assert.That(port1.PortAddress, Is.EqualTo(new PortAddress(0x45, 7, initial1.Universe)), "SwOut[0] not set -> universe unchanged");
            Assert.That(port2.PortAddress, Is.EqualTo(new PortAddress(0x45, 7, 9)), "SwOut[1] belongs to the second port");
        });
    }

    [Test]
    public async Task ResetRestoresTheConfiguredValues()
    {
        receive(node, fromBytes(new ArtAddress(1, (byte)5, (byte)6, new Universe?[] { (byte)7, (byte)8 }, null, null, null, null, null)));
        await waitFor(() => port1.PortAddress == new PortAddress(5, 6, 7) && port2.PortAddress == new PortAddress(5, 6, 8));
        Assert.That(port1.PortAddress, Is.EqualTo(new PortAddress(5, 6, 7)));
        Assert.That(port2.PortAddress, Is.EqualTo(new PortAddress(5, 6, 8)));

        // 0x00 in NetSwitch, SubSwitch and SwOut = reset to the "physical switch setting" (the application's configuration)
        receive(node, fromBytes(new ArtAddress(1, null, null, null, null, null, null, null, null), data =>
        {
            data[12] = 0x00;  // NetSwitch
            data[104] = 0x00; // SubSwitch
            data[100] = 0x00; // SwOut[0]
            data[101] = 0x00; // SwOut[1]
        }));
        await waitFor(() => port1.PortAddress == initial1 && port2.PortAddress == initial2);

        Assert.That(port1.PortAddress, Is.EqualTo(initial1));
        Assert.That(port2.PortAddress, Is.EqualTo(initial2));
    }

    [Test]
    public async Task ApplicationCanVeto()
    {
        int called = 0;
        node.ArtAddressReceived += (_, e) => { Interlocked.Increment(ref called); e.Cancel = true; };

        receive(node, fromBytes(ArtAddress.CreateSetName(1, "Hacked", "Hacked Long")));
        await waitFor(() => Volatile.Read(ref called) == 1);
        await Task.Delay(100);

        Assert.That(called, Is.EqualTo(1));
        Assert.That(node.Name, Is.EqualTo("Original Long"));
        Assert.That(node.ShortName, Is.EqualTo("Original"));
    }

    [Test]
    public async Task ControllerIgnoresArtAddress()
    {
        var controller = new ControllerInstanceMock(artNet, 0x4248) { Name = "Controller Name" };
        artNet.AddInstance(controller);
        int called = 0;
        controller.ArtAddressReceived += (_, _) => Interlocked.Increment(ref called);

        receive(controller, fromBytes(ArtAddress.CreateSetName(1, "Changed", "Changed Long")));
        await Task.Delay(300);

        Assert.That(controller.Name, Is.EqualTo("Controller Name"), "Spec: controllers take no action");
        Assert.That(called, Is.EqualTo(0));
    }

    private void sendDmx(IPv4Address source, PortAddress pa, byte ch1)
    {
        byte[] data = new byte[512];
        data[0] = ch1;
        receive(node, new ArtDMX(0, 1, pa.Net, pa.Address, data), source);
    }

    [Test]
    public async Task MergeModeLtpIsProgrammedAndUsed()
    {
        sendDmx(ipA, initial1, 200);
        sendDmx(ipB, initial1, 10);
        Assert.That(node.GetReceivedDMX(initial1)![0], Is.EqualTo(200), "default HTP");

        receive(node, fromBytes(ArtAddress.CreateSetCommand(1, new ArtAddressCommand(EArtAddressCommand.MergeLtp, 0))));
        await waitFor(() => port1.GoodOutput.MergeMode == EMergeMode.LTP);

        Assert.That(port1.GoodOutput.MergeMode, Is.EqualTo(EMergeMode.LTP), "x = 0 -> first port of the BindIndex");
        Assert.That(port2.GoodOutput.MergeMode, Is.EqualTo(EMergeMode.HTP));
        await Task.Delay(20);
        sendDmx(ipB, initial1, 10); // B is the latest source now
        Assert.That(node.GetReceivedDMX(initial1)![0], Is.EqualTo(10), "LTP: latest source wins");

        receive(node, fromBytes(ArtAddress.CreateSetCommand(1, new ArtAddressCommand(EArtAddressCommand.MergeHtp, 0))));
        await waitFor(() => port1.GoodOutput.MergeMode == EMergeMode.HTP);
        Assert.That(node.GetReceivedDMX(initial1)![0], Is.EqualTo(200), "back to HTP");
    }

    [Test]
    public async Task CancelMergeKeepsOnlyTheNextSource()
    {
        sendDmx(ipA, initial1, 200);
        sendDmx(ipB, initial1, 10);
        Assert.That(port1.GoodOutput.MergingArtNetData, Is.True);

        receive(node, fromBytes(ArtAddress.CreateSetCommand(1, new ArtAddressCommand(EArtAddressCommand.CancelMerge))));
        await Task.Delay(200);

        sendDmx(ipB, initial1, 10);  // the next ArtDmx ends merge mode: B stays
        sendDmx(ipA, initial1, 255); // A is discarded from now on

        Assert.That(node.GetReceivedDMX(initial1)![0], Is.EqualTo(10), "Only B is output");
        Assert.That(port1.GoodOutput.MergingArtNetData, Is.False, "Merge mode ended");
    }
}
