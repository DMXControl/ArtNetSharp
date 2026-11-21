using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using ArtNetTests.Mocks.Instances;
using System.Net;

namespace ArtNetTests;

/// <summary>
/// GoodInput bit 7 ("Data received") of an input port in the ArtPollReply is only set while ArtDmx for that port is
/// actually being sent, not merely because data was written once. Checked from the node's view of the controller.
/// </summary>
[DependsOnFixture(typeof(DmxSendTest))]
public class GoodInputTest
{
    private static readonly PortAddress subscribed = new PortAddress(0, 0, 11);
    private static readonly PortAddress unsubscribed = new PortAddress(0, 0, 12);

    [Test, CancelAfter(30_000)]
    public async Task DataReceivedOnlyWhileTransmitting(CancellationToken cancellationToken)
    {
        var artNet = new ArtNet();
        try
        {
            foreach (var nic in artNet.NetworkClients)
                if (!IPAddress.IsLoopback(nic.LocalIpAddress))
                    nic.Enabled = false;

            var node = new NodeMock(artNet) { Name = "GoodInput Node" };
            node.AddPortConfig(new PortConfig(1, subscribed, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet });
            var controller = new ControllerInstanceMock(artNet, 0x4247) { Name = "GoodInput Controller" };
            controller.AddPortConfig(new PortConfig(1, subscribed, false, true) { PortNumber = 1, Type = EPortType.InputToArtNet });
            controller.AddPortConfig(new PortConfig(2, unsubscribed, false, true) { PortNumber = 1, Type = EPortType.InputToArtNet });
            artNet.AddInstance(node, controller);

            // Data is written for both input ports, but only "subscribed" has a receiver (the node).
            byte[] data = new byte[512];
            data[0] = 1;
            controller.WriteDMXValues(subscribed, data);
            controller.WriteDMXValues(unsubscribed, data);

            RemoteClientPort? port(PortAddress pa) => node.RemoteClients?
                .FirstOrDefault(rc => rc.LongName == "GoodInput Controller")?.Ports
                .FirstOrDefault(p => p.InputPortAddress == pa);

            // Wait until the node got a controller reply that was built after DMX started flowing.
            while (port(subscribed)?.GoodInput.DataReceived != true)
                await Task.Delay(100, cancellationToken);

            Assert.That(port(subscribed)!.GoodInput.DataReceived, Is.True, "Data is sent for this port");
            Assert.That(port(unsubscribed)?.GoodInput.DataReceived, Is.Not.True,
                "No subscriber, so nothing is sent for this port - it must not report 'Data received'");
        }
        finally
        {
            ((IDisposable)artNet).Dispose();
        }
    }
}
