using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using System.Net;
using System.Net.Sockets;

namespace ArtNetTests;

/// <summary>
/// Regression test for GitHub issue #11: on Linux and macOS a socket bound to the
/// interface's unicast IP does not receive subnet broadcasts, so nodes were not
/// discoverable and did not receive broadcast ArtDmx there.
/// </summary>
[DependsOnFixture(typeof(TestOS))]
public class BroadcastReceiveTest
{
    private ArtNet artNet;

    [OneTimeSetUp]
    public void OneTimeSetUp() => artNet = new ArtNet();

    [OneTimeTearDown]
    public void OneTimeTearDown() => ((IDisposable)artNet)?.Dispose();

    [Test, CancelAfter(30_000)]
    public async Task ReceivesSubnetBroadcast(CancellationToken cancellationToken)
    {
        ArtNet.NetworkClientBag? nic = null;
        for (int i = 0; i < 50 && nic == null; i++)
        {
            nic = artNet.NetworkClients.FirstOrDefault(n =>
                n.ClientAlive
                && n.LocalIpAddress.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(n.LocalIpAddress)
                && !n.BroadcastIpAddress.Equals(n.LocalIpAddress));
            if (nic == null)
                await Task.Delay(100, cancellationToken);
        }
        if (nic == null)
            Assert.Ignore("No non-loopback IPv4 interface with a broadcast address available");

        Console.WriteLine($"NIC: {nic!.LocalIpAddress}, Broadcast: {nic.BroadcastIpAddress}");
        if (nic.NeedsBroadcastClient)
            Assert.That(nic.BroadcastClientAlive, Is.True, "Broadcast receiver socket was not bound");

        var controller = new ControllerInstanceMock(artNet, 0x4243) { Name = "Broadcast Test Controller" };
        artNet.AddInstance(controller);

        const string longName = "Broadcast Test Node";
        using var reply = new ArtPollReply(
            nic.LocalIpAddress,
            null,
            new MACAddress(2, 0, 0, 0, 0, 11),
            "BcTest",
            longName,
            0,
            new NodeStatus(),
            1,
            0,
            0,
            0,
            new object[0],
            new object[0],
            0x1111,
            0x2222,
            style: EStCodes.StNode);
        byte[] data = reply.GetPacket();

        using var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { EnableBroadcast = true };
        sender.Bind(new IPEndPoint(nic.LocalIpAddress, 0));
        var destination = new IPEndPoint(nic.BroadcastIpAddress, Constants.ARTNET_PORT);

        while (controller.RemoteClients?.Any(rc => longName.Equals(rc?.LongName)) != true)
        {
            await sender.SendToAsync(data, SocketFlags.None, destination, cancellationToken);
            await Task.Delay(500, cancellationToken);
        }

        Assert.That(controller.RemoteClients.Count(rc => longName.Equals(rc?.LongName)), Is.EqualTo(1));
    }
}
