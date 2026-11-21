using ArtNetSharp;
using ArtNetSharp.Communication;
using org.dmxc.wkdt.Light.RDM;

namespace ArtNetTests;

/// <summary>
/// B7a: a timed-out port is parked, not deleted. When it reports again (e.g. after a debugger pause longer than
/// the timeout) the same object comes back with its state (RDM responders) intact.
/// </summary>
[NonParallelizable]
public class PortTimeoutTest
{
    private static readonly IPv4Address nodeIp = new IPv4Address("10.0.0.20");
    private static readonly PortAddress portAddress = new PortAddress(0, 0, 4);
    private TimeSpan originalTimeout;

    [SetUp]
    public void SetUp()
    {
        originalTimeout = RemoteClientPort.Timeout;
        RemoteClientPort.Timeout = TimeSpan.FromMilliseconds(200);
    }

    [TearDown]
    public void TearDown() => RemoteClientPort.Timeout = originalTimeout;

    private static ArtPollReply portReply() => new ArtPollReply(nodeIp,
                                                               nodeIp,
                                                               new MACAddress(2, 0, 0, 0, 0, 20),
                                                               "TimeoutNode",
                                                               "Port Timeout Node",
                                                               1,
                                                               new NodeStatus(),
                                                               1,
                                                               0,
                                                               portAddress.Net,
                                                               portAddress.Subnet,
                                                               portAddress.Universe,
                                                               (Universe)0,
                                                               0x1111,
                                                               0x2222,
                                                               null,
                                                               EPortType.OutputFromArtNet,
                                                               default,
                                                               default,
                                                               style: EStCodes.StNode);

    [Test]
    public async Task TimedOutPortIsParkedAndRevivedAsSameObject()
    {
        using var reply = portReply();
        var client = new RemoteClient(reply);
        Assert.That(client.Ports, Has.Count.EqualTo(1));
        RemoteClientPort port = client.Ports.First();
        port.AddResponderRdmUIDs(portAddress, 1, new UID(0x1234, 1), new UID(0x1234, 2));

        var timedOut = new List<RemoteClientPort>();
        var discovered = new List<RemoteClientPort>();
        client.PortTimedOut += (_, p) => { lock (timedOut) timedOut.Add(p); };
        client.PortDiscovered += (_, p) => { lock (discovered) discovered.Add(p); };

        // "Debugger pause": no reply for longer than the timeout
        await Task.Delay(300);
        client.RemoveTimedOutPorts();
        await Task.Delay(100); // events are raised asynchronously

        Assert.That(client.Ports, Is.Empty, "Timed-out port is no longer listed");
        Assert.That(timedOut, Has.Count.EqualTo(1));
        Assert.That(timedOut[0], Is.SameAs(port), "PortTimedOut must pass the RemoteClientPort itself");

        // The node reports again
        using var again = portReply();
        client.processArtPollReply(again);
        await Task.Delay(100);

        Assert.That(client.Ports, Has.Count.EqualTo(1));
        Assert.That(client.Ports.First(), Is.SameAs(port), "The same object must come back");
        Assert.That(port.KnownResponderRDMUIDs, Has.Count.EqualTo(2), "Known RDM responders must survive the outage");
        Assert.That(discovered, Has.Count.EqualTo(1));
        Assert.That(discovered[0], Is.SameAs(port));

        // A port that is alive is not reported again
        client.RemoveTimedOutPorts();
        await Task.Delay(100);
        Assert.That(timedOut, Has.Count.EqualTo(1));
    }
}
