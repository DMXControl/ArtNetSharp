using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetSharp.Misc;
using ArtNetTests.Mocks;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;
using System.Diagnostics;

namespace ArtNetTests;

/// <summary>
/// RDM over Art-Net end to end, controller and node in one ArtNet connected by the loop adapter:
/// discovery of the node, ToD request and flush (ArtTodRequest/ArtTodControl -> ArtTodData),
/// an RDM request (SendArtRDM) answered by the node and the response arriving at the controller,
/// and the bookkeeping of controller/responder UIDs on the remote ports.
/// </summary>
[NonParallelizable]
public class RdmEndToEndTest
{
    private static readonly PortAddress universe = new PortAddress(0, 1, 2);
    private static readonly UID responder1 = new UID(0x1234, 0x0001);
    private static readonly UID responder2 = new UID(0x1234, 0x0002);
    private ArtNet artNet;
    private NodeInstance node;
    private ControllerInstanceMock controller;

    [SetUp]
    public async Task SetUp()
    {
        artNet = new ArtNet();
        artNet.LoopNetwork = new ArtNet.NetworkLoopAdapter(new IPv4Address("255.0.0.0"));
        foreach (var nic in artNet.NetworkClients)
            nic.Enabled = false; // only the loop adapter

        node = new NodeInstance(artNet, supportRDM: true) { Name = "RDM Node", ShortName = "RDM Node" };
        var nodePort = new PortConfig(1, universe, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet };
        nodePort.AddAdditionalRdmUIDs(responder1, responder2);
        node.AddPortConfig(nodePort);

        controller = new ControllerInstanceMock(artNet, 0x4250) { Name = "RDM Controller" };
        controller.AddPortConfig(new PortConfig(1, universe, false, true) { PortNumber = 1, Type = EPortType.InputToArtNet });
        artNet.AddInstance(node, controller);

        // Both directions: the node learns about the controller from the controller's reply to its own poll,
        // which has its own random delay - so it can come later than the node's reply.
        await waitFor(() => nodePortSeenByController() != null && controllerPortSeenByNodeNow() != null, 10_000);
        Assert.That(nodePortSeenByController(), Is.Not.Null, "Controller did not discover the node's RDM port");
        Assert.That(controllerPortSeenByNodeNow(), Is.Not.Null, "Node did not discover the controller's port");
    }

    private RemoteClientPort? controllerPortSeenByNodeNow() => node.RemoteClientsPorts?.FirstOrDefault(p => p.InputPortAddress == universe);

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    private RemoteClientPort? nodePortSeenByController() => controller.RemoteClientsPorts?
        .FirstOrDefault(p => p.OutputPortAddress == universe && p.IsRDMCapable);

    private static async Task waitFor(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(20);
    }

    [Test]
    public async Task TodRequestDeliversTheResponders()
    {
        await controller.PerformRDMDiscovery(universe);
        await waitFor(() => nodePortSeenByController()!.KnownResponderRDMUIDs.Count == 2);

        Assert.That(nodePortSeenByController()!.KnownResponderRDMUIDs.Select(b => b.Uid), Is.EquivalentTo(new[] { responder1, responder2 }));
        Assert.That(controller.KnownRDMUIDs.Select(b => b.Uid), Is.SupersetOf(new[] { responder1, responder2 }));
        Assert.That(controller.RemoteClients.First(rc => rc.LongName == "RDM Node").KnownRDMUIDs.Select(b => b.Uid),
            Is.EquivalentTo(new[] { responder1, responder2 }), "The RemoteClient aggregates the UIDs of its ports");
    }

    [Test]
    public async Task TodFlushDeliversTheResponders()
    {
        await controller.PerformRDMDiscovery(universe, flush: true); // ArtTodControl AtcFlush
        await waitFor(() => nodePortSeenByController()!.KnownResponderRDMUIDs.Count == 2);
        Assert.That(nodePortSeenByController()!.KnownResponderRDMUIDs, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task RequestIsAnsweredAndTheResponseArrives()
    {
        await controller.PerformRDMDiscovery(universe);
        await waitFor(() => nodePortSeenByController()!.KnownResponderRDMUIDs.Count == 2);

        RDMMessage? requestAtNode = null;
        node.RequestRDMMessageReceived += (_, e) =>
        {
            requestAtNode = e.Request;
            e.SetResponse(new RDMMessage
            {
                Command = ERDM_Command.GET_COMMAND_RESPONSE,
                Parameter = e.Request.Parameter,
                SourceUID = e.Request.DestUID,
                DestUID = e.Request.SourceUID,
                TransactionCounter = e.Request.TransactionCounter,
            });
        };
        ResponseRDMMessageReceivedEventArgs? responseAtController = null;
        controller.ResponseRDMMessageReceived += (_, e) => responseAtController = e;

        await controller.SendArtRDM(new RDMMessage
        {
            Command = ERDM_Command.GET_COMMAND,
            Parameter = ERDM_Parameter.DEVICE_INFO,
            DestUID = responder1,
            TransactionCounter = 5,
        });

        await waitFor(() => responseAtController != null);

        Assert.That(requestAtNode, Is.Not.Null, "The node got the request");
        Assert.That(requestAtNode!.SourceUID, Is.EqualTo(controller.UID), "SendArtRDM fills in the controller's UID");
        Assert.That(responseAtController, Is.Not.Null, "The controller got the response to its own request");
        Assert.That(responseAtController!.Response.SourceUID, Is.EqualTo(responder1));

        // Bookkeeping on the remote ports: the node knows the controller UID on the controller's (input) port.
        var controllerPortSeenByNode = controllerPortSeenByNodeNow();
        Assert.That(controllerPortSeenByNode, Is.Not.Null);
        Assert.That(controllerPortSeenByNode!.KnownControllerRDMUIDs.Select(b => b.Uid), Does.Contain(controller.UID));
    }

    [Test]
    public async Task RequestWaitsWhileTheGatewayQueueIsFull()
    {
        await controller.PerformRDMDiscovery(universe);
        await waitFor(() => nodePortSeenByController()!.KnownResponderRDMUIDs.Count == 2);

        var original = AbstractInstance.GatewayRDMFiFoQueue.MaxWait;
        AbstractInstance.GatewayRDMFiFoQueue.MaxWait = TimeSpan.FromMilliseconds(600);
        try
        {
            // The gateway (node, loop address) reports a full queue: 0 of 1 entries free.
            var gatewayIp = nodePortSeenByController()!.IpAddress;
            using (var full = new ArtRDM(universe, new RDMMessage
            {
                Command = ERDM_Command.GET_COMMAND_RESPONSE,
                Parameter = ERDM_Parameter.DEVICE_INFO,
                SourceUID = responder2,
                DestUID = controller.UID,
                TransactionCounter = 200,
            }, fifoAvailable: 0, fifoMax: 1))
                ((IInstance)controller).PacketReceived(full, gatewayIp, gatewayIp);
            Assert.That(controller.GetGatewayRdmQueue(gatewayIp)!.Implemented, Is.True);

            var sw = Stopwatch.StartNew();
            long receivedAfter = -1;
            node.RequestRDMMessageReceived += (_, _) => Interlocked.CompareExchange(ref receivedAfter, sw.ElapsedMilliseconds, -1);
            await controller.SendArtRDM(new RDMMessage
            {
                Command = ERDM_Command.GET_COMMAND,
                Parameter = ERDM_Parameter.DEVICE_INFO,
                DestUID = responder1,
                TransactionCounter = 9,
            });
            await waitFor(() => Interlocked.Read(ref receivedAfter) >= 0);

            Assert.That(Interlocked.Read(ref receivedAfter), Is.GreaterThanOrEqualTo(500),
                "The request waited for a free entry (none came, so it went out after MaxWait)");
        }
        finally
        {
            AbstractInstance.GatewayRDMFiFoQueue.MaxWait = original;
        }
    }

    [Test]
    public async Task RequestToUnknownUidIsNotSent()
    {
        int requests = 0;
        node.RequestRDMMessageReceived += (_, _) => Interlocked.Increment(ref requests);

        await controller.SendArtRDM(new RDMMessage
        {
            Command = ERDM_Command.GET_COMMAND,
            Parameter = ERDM_Parameter.DEVICE_INFO,
            DestUID = new UID(0x7777, 0x7777), // nobody reported this responder
        });
        await Task.Delay(300);

        Assert.That(requests, Is.EqualTo(0));
    }

    [Test]
    public async Task OutdatedUidsAreRemoved()
    {
        await controller.PerformRDMDiscovery(universe);
        await waitFor(() => nodePortSeenByController()!.KnownResponderRDMUIDs.Count == 2);
        var client = controller.RemoteClients.First(rc => rc.LongName == "RDM Node");
        var port = nodePortSeenByController()!;

        var original = RDMUID_ReceivedBag.Timeout;
        RDMUID_ReceivedBag.Timeout = TimeSpan.FromMilliseconds(100);
        try
        {
            await Task.Delay(200);
            Assert.That(port.GetReceivedRDMUIDs(), Is.Empty, "Timed-out UIDs are not reported any more");

            port.RemoveOutdatedRdmUIDs();
            client.RemoveOutdatedRdmUIDs();
            controller.RemoveOutdatedRdmUIDs();

            Assert.Multiple(() =>
            {
                Assert.That(port.KnownResponderRDMUIDs, Is.Empty);
                Assert.That(client.KnownRDMUIDs, Is.Empty);
                Assert.That(controller.KnownRDMUIDs, Is.Empty);
            });
        }
        finally
        {
            RDMUID_ReceivedBag.Timeout = original;
        }
    }
}
