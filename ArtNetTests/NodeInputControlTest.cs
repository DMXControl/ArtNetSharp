using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using ArtNetTests.Mocks.Instances;
using System.Diagnostics;

namespace ArtNetTests;

/// <summary>
/// ArtInput: a controller disables / enables DMX inputs of a node. A disabled input reports GoodInput "Input disabled"
/// and sends no ArtDmx. Two nodes in one ArtNet over the loop adapter: "source" inputs DMX, "receiver" outputs it.
/// (Not named ArtInput*Test: NUnit's DependsOnFixture handling skipped the fixture chain with such names.)
/// </summary>
[NonParallelizable]
public class NodeInputControlTest
{
    private static readonly PortAddress universe = new PortAddress(0, 0, 15);
    private static readonly IPv4Address loopIp = new IPv4Address("3.3.3.3");
    private ArtNet artNet;
    private NodeInstance source;
    private NodeMock receiver;
    private PortConfig sourcePort;

    [SetUp]
    public async Task SetUp()
    {
        artNet = new ArtNet();
        artNet.LoopNetwork = new ArtNet.NetworkLoopAdapter(new IPv4Address("255.0.0.0"));
        foreach (var nic in artNet.NetworkClients)
            nic.Enabled = false;

        source = new NodeInstance(artNet) { Name = "Input Node" };
        sourcePort = new PortConfig(1, universe, false, true) { PortNumber = 1, Type = EPortType.InputToArtNet };
        source.AddPortConfig(sourcePort);
        receiver = new NodeMock(artNet) { Name = "Output Node" };
        receiver.AddPortConfig(new PortConfig(1, universe, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet });
        artNet.AddInstance(source, receiver);

        // the input node discovers its subscriber and starts sending
        write(1);
        Assert.That(await within(() => receiver.GetReceivedDMX(universe)?[0] == 1, 8000), Is.True, "DMX flows before the test");
    }

    [TearDown]
    public void TearDown() => ((IDisposable)artNet)?.Dispose();

    private void write(byte value)
    {
        byte[] data = new byte[512];
        data[0] = value;
        source.WriteDMXValues(universe, data);
    }

    private void receive(AbstractInstance instance, ArtInput artInput)
    {
        using (artInput)
            ((IInstance)instance).PacketReceived(artInput, loopIp, loopIp);
    }

    private static async Task<bool> within(Func<bool> condition, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(10);
        return condition();
    }

    [Test]
    public async Task DisabledInputSendsNothingUntilEnabledAgain()
    {
        receive(source, new ArtInput(1, [EArtInputCommand.DisableInput]));
        Assert.That(await within(() => sourcePort.GoodInput.InputDisabled, 2000), Is.True, "GoodInput reports 'Input disabled'");
        await Task.Delay(100); // a frame already in flight may still arrive

        write(2);
        Assert.That(await within(() => receiver.GetReceivedDMX(universe)?[0] == 2, 1000), Is.False, "No ArtDmx from a disabled input");

        receive(source, new ArtInput(1, [EArtInputCommand.None]));
        Assert.That(await within(() => !sourcePort.GoodInput.InputDisabled, 2000), Is.True);
        Assert.That(await within(() => receiver.GetReceivedDMX(universe)?[0] == 2, 2000), Is.True, "Enabled again: data flows");
    }

    [Test]
    public async Task ApplicationCanVeto()
    {
        int called = 0;
        source.ArtInputReceived += (_, e) => { Interlocked.Increment(ref called); e.Cancel = true; };

        receive(source, new ArtInput(1, [EArtInputCommand.DisableInput]));
        Assert.That(await within(() => Volatile.Read(ref called) == 1, 2000), Is.True);
        await Task.Delay(100);

        Assert.That(sourcePort.GoodInput.InputDisabled, Is.False);
        write(3);
        Assert.That(await within(() => receiver.GetReceivedDMX(universe)?[0] == 3, 2000), Is.True);
    }

    [Test]
    public async Task ControllerIgnoresArtInput()
    {
        var controller = new ControllerInstanceMock(artNet, 0x4254) { Name = "Input Controller" };
        var controllerPort = new PortConfig(2, new PortAddress(0, 1, 0), false, true) { PortNumber = 1, Type = EPortType.InputToArtNet };
        controller.AddPortConfig(controllerPort);
        artNet.AddInstance(controller);
        int called = 0;
        controller.ArtInputReceived += (_, _) => Interlocked.Increment(ref called);

        receive(controller, new ArtInput(2, [EArtInputCommand.DisableInput]));
        await Task.Delay(300);

        Assert.That(called, Is.EqualTo(0), "Spec: controllers take no action");
        Assert.That(controllerPort.GoodInput.InputDisabled, Is.False);
    }

    [Test]
    public void EveryPortIsEncodedInThePacket()
    {
        EArtInputCommand[] inputs = [EArtInputCommand.None, EArtInputCommand.DisableInput, EArtInputCommand.None, EArtInputCommand.DisableInput];
        using var created = new ArtInput(1, inputs, 4);
        using var parsed = new ArtInput(created.GetPacket());
        // fillPacket wrote every port to byte 16 before, so only the last value arrived - as port 0
        Assert.That(parsed.Input, Is.EqualTo(inputs));
    }
}
