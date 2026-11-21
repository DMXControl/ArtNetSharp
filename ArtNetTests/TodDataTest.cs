using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using ArtNetTests.Mocks.Instances;
using org.dmxc.wkdt.Light.RDM;

namespace ArtNetTests;

/// <summary>
/// ArtTodData (Spec): TodFull for every block of a ToD (BlockCount 0,1,2,... with up to 200 UIDs each),
/// an empty ToD is a TodFull with UidTotal 0, TodNak means "ToD not available".
/// </summary>
public class TodDataTest
{
    private static readonly PortAddress portAddress = new PortAddress(0, 0, 9);
    private static readonly IPv4Address localIp = new IPv4Address("10.0.0.1");
    private static readonly IPv4Address sourceIp = new IPv4Address("10.0.0.2");
    private ArtNet artNet;
    private ControllerInstanceMock controller;

    [SetUp]
    public void SetUp()
    {
        artNet = new ArtNet();
        controller = new ControllerInstanceMock(artNet, 0x4245) { Name = "ToD Test Controller" };
        artNet.AddInstance(controller);
    }

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    private static UID[] uids(int count, uint offset = 0) => Enumerable.Range(1, count).Select(i => new UID(0x1234, (uint)i + offset)).ToArray();

    private void receive(ArtTodData tod)
    {
        using (tod)
            ((IInstance)controller).PacketReceived(tod, localIp, sourceIp);
    }

    [Test]
    public async Task LargeTodIsSentAsTodFullBlocks()
    {
        // The node answers an ArtTodRequest over the loop adapter; the controller in the same ArtNet receives the blocks.
        artNet.LoopNetwork = new ArtNet.NetworkLoopAdapter(new IPv4Address("255.0.0.0"));
        var node = new NodeMock(artNet) { Name = "ToD Test Node" };
        var config = new PortConfig(1, portAddress, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet };
        config.AddAdditionalRdmUIDs(uids(450)); // 3 blocks: 200 + 200 + 50
        node.AddPortConfig(config);
        artNet.AddInstance(node);

        using var request = new ArtTodRequest(portAddress);
        ((IInstance)node).PacketReceived(request, localIp, sourceIp);

        for (int i = 0; i < 100 && controller.KnownRDMUIDs.Count < 450; i++)
            await Task.Delay(50);

        Assert.That(controller.KnownRDMUIDs, Has.Count.EqualTo(450), "Blocks after the first must be TodFull, not TodNak");
    }

    [Test]
    public void TodFullBlocksAreAllAccepted()
    {
        var all = uids(250);
        receive(new ArtTodData(portAddress, 1, 1, 250, 0, all.Take(200).ToArray(), EArtTodDataCommandResponse.TodFull));
        receive(new ArtTodData(portAddress, 1, 1, 250, 1, all.Skip(200).ToArray(), EArtTodDataCommandResponse.TodFull));
        Assert.That(controller.KnownRDMUIDs, Has.Count.EqualTo(250));
    }

    [Test]
    public void TodNakKeepsKnownDevices()
    {
        receive(new ArtTodData(portAddress, 1, 1, 3, 0, uids(3), EArtTodDataCommandResponse.TodFull));
        receive(new ArtTodData(portAddress, 1, 1, 0, 0, Array.Empty<UID>(), EArtTodDataCommandResponse.TodNak));
        Assert.That(controller.KnownRDMUIDs, Has.Count.EqualTo(3), "TodNak = ToD not available, must not clear the list");
    }

    [Test]
    public void EmptyTodClearsKnownDevices()
    {
        var otherPort = new PortAddress(0, 0, 10);
        receive(new ArtTodData(portAddress, 1, 1, 3, 0, uids(3), EArtTodDataCommandResponse.TodFull));
        receive(new ArtTodData(otherPort, 1, 2, 2, 0, uids(2, 100), EArtTodDataCommandResponse.TodFull));
        receive(new ArtTodData(portAddress, 1, 1, 0, 0, Array.Empty<UID>(), EArtTodDataCommandResponse.TodFull));
        Assert.That(controller.KnownRDMUIDs.Select(b => b.PortAddress), Is.All.EqualTo(otherPort),
            "An empty TodFull removes the devices of that Port-Address only");
        Assert.That(controller.KnownRDMUIDs, Has.Count.EqualTo(2));
    }
}
