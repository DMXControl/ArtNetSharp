using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using ArtNetTests.Mocks.Instances;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ArtNetTests;

[DependsOnFixture(typeof(PackagesSerializeDeserialize))]
public class TestOS
{
    private static readonly ILogger Logger = ArtNetSharp.Logging.CreateLogger<TestOS>();
    private NodeInstance nodeInstance;
    private ControllerInstance controllerInstance;
    private const byte ports = 2;
    private ArtNet artNet;
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        artNet = new ArtNet();
    }
    [SetUp]
    public void SetUp()
    {
        nodeInstance = new NodeMock(artNet);
        nodeInstance.Name = "Test Node";
        controllerInstance = new ControllerInstanceMock(artNet, 0x42);
        controllerInstance.Name = "Test Controller";
        for (ushort i = 1; i <= ports; i++)
        {
            nodeInstance.AddPortConfig(new PortConfig((byte)i, i, true, false) { PortNumber = (byte)i, Type = EPortType.OutputFromArtNet, GoodOutput = new GoodOutput(outputStyle: GoodOutput.EOutputStyle.Continuous, isBeingOutputAsDMX: true) });
            controllerInstance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet });
        }
    }
    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (artNet != null)
            ((IDisposable)artNet).Dispose();
        Trace.Flush();
        await Task.Delay(6500);
    }

    [Test, CancelAfter(90_000)]
    public async Task TestOnLinux(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("Skiped, only run on Linux");

        Logger.LogDebug(nameof(TestOnLinux));
        await doTests(cancellationToken);
    }

    [Test, CancelAfter(90_000)]
    public async Task TestOnWindows(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            Assert.Ignore("Skiped, only run on Windows");

        Logger.LogDebug(nameof(TestOnWindows));
        await doTests(cancellationToken);
    }

    [Test, CancelAfter(90_000)]
    public async Task TestOnMacOS(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
            Assert.Ignore("Skiped, only run on Mac OS");

        Logger.LogDebug(nameof(TestOnMacOS));
        await doTests(cancellationToken);
    }

    private async Task doTests(CancellationToken cancellationToken = default)
    {
        foreach (var nic in artNet.NetworkClients)
        {
            var str = $"NIC: {nic.LocalIpAddress}";
            Console.WriteLine(str);
            Debug.WriteLine(str);
            if (nic.BroadcastIpAddress != System.Net.IPAddress.Loopback)
                nic.Enabled = false;
        }

        artNet.AddInstance(nodeInstance);
        artNet.AddInstance(controllerInstance);

        while (controllerInstance.RemoteClients?.FirstOrDefault(rc => string.Equals(nodeInstance.Name, rc?.LongName))?.Ports?.Count != ports)
            await Task.Delay(1000, cancellationToken);

        var nodeRD = controllerInstance.RemoteClients.FirstOrDefault(rc => nodeInstance.Name.Equals(rc?.LongName));
        Assert.That(nodeRD, Is.Not.Null);
        Assert.That(nodeRD.Ports, Has.Count.EqualTo(ports));
    }
}