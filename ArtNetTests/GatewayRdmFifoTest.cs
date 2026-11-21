using ArtNetSharp;
using ArtNetSharp.Communication;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;
using System.Diagnostics;

namespace ArtNetTests;

/// <summary>
/// RDM transmit queue of a gateway (ArtRdm FifoAvail / FifoMax, Art-Net 4 Revision DL):
/// no throttling without a queue (FifoMax = 0), entries handed out in order, waiting requests released by the
/// gateway's reports, and "send anyway" after MaxWait.
/// </summary>
[NonParallelizable]
public class GatewayRdmFifoTest
{
    private static readonly IPv4Address gatewayIp = new IPv4Address("10.0.0.9");
    private TimeSpan originalMaxWait;

    [SetUp]
    public void SetUp() => originalMaxWait = AbstractInstance.GatewayRDMFiFoQueue.MaxWait;

    [TearDown]
    public void TearDown() => AbstractInstance.GatewayRDMFiFoQueue.MaxWait = originalMaxWait;

    /// <summary>An ArtRdm response from the gateway reporting its queue state.</summary>
    private static ArtRDM gatewayReport(byte available, byte max, ERDM_Command command = ERDM_Command.GET_COMMAND_RESPONSE)
        => new ArtRDM(new PortAddress(0, 0, 1), new RDMMessage
        {
            Command = command,
            Parameter = ERDM_Parameter.DEVICE_INFO,
            SourceUID = new UID(0x1234, 1),
            DestUID = new UID(0x02b0, 12314),
        }, fifoAvailable: available, fifoMax: max);

    private static void report(AbstractInstance.GatewayRDMFiFoQueue queue, byte available, byte max)
    {
        using var packet = gatewayReport(available, max);
        queue.Update(packet);
    }

    [Test]
    public void WithoutQueueNothingIsThrottled()
    {
        var queue = new AbstractInstance.GatewayRDMFiFoQueue(gatewayIp);
        report(queue, 0, 0); // what every known device reports
        for (int i = 0; i < 50; i++)
            Assert.That(queue.AcquireSlotAsync().IsCompletedSuccessfully, Is.True);
        Assert.That(queue.Implemented, Is.False);
    }

    [Test]
    public async Task FreeEntriesAreUsedAndWaitersReleasedInOrder()
    {
        var queue = new AbstractInstance.GatewayRDMFiFoQueue(gatewayIp);
        report(queue, available: 2, max: 4);
        Assert.That(queue.Implemented, Is.True);

        Assert.That(queue.AcquireSlotAsync().IsCompletedSuccessfully, Is.True);
        Assert.That(queue.AcquireSlotAsync().IsCompletedSuccessfully, Is.True);
        Assert.That(queue.Available, Is.EqualTo(0));

        Task a = queue.AcquireSlotAsync(), b = queue.AcquireSlotAsync(), c = queue.AcquireSlotAsync();
        await Task.Delay(50);
        Assert.That(new[] { a, b, c }.Any(t => t.IsCompleted), Is.False, "Queue full: requests wait");

        report(queue, available: 2, max: 4);
        await Task.WhenAll(a, b);
        await Task.Delay(50);
        Assert.That(c.IsCompleted, Is.False, "Only as many as the gateway reported free, oldest first");

        report(queue, available: 1, max: 4);
        await c;
    }

    [Test]
    public async Task FullQueueSendsAnywayAfterMaxWait()
    {
        AbstractInstance.GatewayRDMFiFoQueue.MaxWait = TimeSpan.FromMilliseconds(200);
        var queue = new AbstractInstance.GatewayRDMFiFoQueue(gatewayIp);
        report(queue, available: 0, max: 2);

        var sw = Stopwatch.StartNew();
        await queue.AcquireSlotAsync(); // e.g. a response got lost: must not block for good
        Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(150));
    }

    [Test]
    public async Task QueueSwitchedOffReleasesWaiters()
    {
        var queue = new AbstractInstance.GatewayRDMFiFoQueue(gatewayIp);
        report(queue, available: 0, max: 2);
        var waiting = queue.AcquireSlotAsync();
        await Task.Delay(50);
        Assert.That(waiting.IsCompleted, Is.False);

        report(queue, available: 0, max: 0); // gateway no longer reports a queue
        await waiting;
        Assert.That(queue.Implemented, Is.False);
    }

    [Test]
    public void RequestsDoNotChangeTheQueueState()
    {
        var queue = new AbstractInstance.GatewayRDMFiFoQueue(gatewayIp);
        report(queue, available: 3, max: 4);
        using (var request = gatewayReport(0, 0, ERDM_Command.GET_COMMAND)) // a request carries no gateway state
            queue.Update(request);
        Assert.That(queue.Max, Is.EqualTo(4));
        Assert.That(queue.Available, Is.EqualTo(3));
    }

    [Test]
    public async Task InstanceTracksTheQueueOfEachGateway()
    {
        using var artNet = new ArtNet();
        var controller = new ArtNetTests.Mocks.ControllerInstanceMock(artNet, 0x4253) { Name = "FIFO Controller" };
        artNet.AddInstance(controller);
        Assert.That(controller.GetGatewayRdmQueue(gatewayIp), Is.Null);

        using (var response = new ArtRDM(gatewayReport(3, 8).GetPacket())) // round trip like from the network
            ((IInstance)controller).PacketReceived(response, new IPv4Address("10.0.0.1"), gatewayIp);
        await Task.Delay(100);

        var queue = controller.GetGatewayRdmQueue(gatewayIp);
        Assert.That(queue, Is.Not.Null);
        Assert.That(queue!.Max, Is.EqualTo(8));
        Assert.That(queue.Available, Is.EqualTo(3));
    }
}
