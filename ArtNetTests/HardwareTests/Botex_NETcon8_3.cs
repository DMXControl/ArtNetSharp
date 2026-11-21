using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetSharp.Misc;
using ArtNetTests.Mocks;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;

namespace ArtNetTests.HardwareTests;

public struct Botex_NETcon8_3TestSubject
{
    public static readonly object[] TestSubjects = new object[]
    {
        //Patrick Grote, HomeLab: 21.11.2026, FW 2.6.7, 3 RDM responders on output 1 (Universe 1)
        new Botex_NETcon8_3TestSubject() { IP = new IPv4Address("2.168.15.11"), MAC = new MACAddress("e0:fe:83:e0:c4:64"), LongName = "ArtNet-DMX/RDM-8Ports", RdmUniverse = new PortAddress(0, 0, 1) },
    };
    public MACAddress MAC { get; private set; }
    public IPv4Address IP { get; private set; }
    public string LongName { get; private set; }
    /// <summary>Port-Address of the output with RDM responders.</summary>
    public PortAddress RdmUniverse { get; private set; }
    public override readonly string ToString()
    {
        return $"Botex NETcon8-3 ({MAC}) on IP: {IP}";
    }
}

/// <summary>
/// Botex NETcon8-3: 8 DMX outputs, announced as two ArtPollReplies (BindIndex 1 and 2) with 4 ports each.
/// RDM over the real network with RDMSharp messages: ToD of one output and GET requests to its responders.
/// </summary>
[Category("Hardware")]
[DependsOnFixture(typeof(TestOS))]
[TestFixtureSource(typeof(Botex_NETcon8_3TestSubject), nameof(Botex_NETcon8_3TestSubject.TestSubjects))]
public class Botex_NETcon8_3
{
    private bool? Pingable;
    private ArtNet artNet;
    private readonly Botex_NETcon8_3TestSubject testSubject;
    private RemoteClient? remoteClient;
    private ControllerInstanceMock instance;
    private readonly ConcurrentQueue<ResponseRDMMessageReceivedEventArgs> responses = new();

    private static Tuple<IPv4Address, IPv4Address>[] IPs => Tools.GetIpAddresses();

    public Botex_NETcon8_3(Botex_NETcon8_3TestSubject testSubject)
    {
        this.testSubject = testSubject;
    }

    [OneTimeSetUp]
    public async Task SetUp()
    {
        if (!await IsPingable())
            return;

        artNet = new ArtNet();
        instance = new ControllerInstanceMock(artNet, 0x1235) { Name = $"Test: {nameof(Botex_NETcon8_3)}" };
        instance.AddPortConfig(new PortConfig(1, testSubject.RdmUniverse, false, true) { PortNumber = 1, Type = EPortType.InputToArtNet | EPortType.ArtNet });
        instance.ResponseRDMMessageReceived += (_, e) => responses.Enqueue(e);
        artNet.AddInstance(instance);

        // both ArtPollReplies (BindIndex 1 and 2) received
        for (int i = 0; i < 1000; i++)
        {
            remoteClient ??= instance.RemoteClients?.FirstOrDefault(rc => testSubject.MAC.Equals(rc.MacAddress));
            if (remoteClient?.Ports.Select(p => p.BindIndex).Distinct().Count() >= 2)
                return;
            await Task.Delay(10);
        }
    }

    private async Task<bool> IsPingable()
    {
        if (ArtNetSharp.Tools.IsRunningOnGithubWorker())
            Pingable = false;

        if (!Pingable.HasValue)
        {
            if (!IPs.Any(nc => ArtNetSharp.Tools.IsInSubnet(nc.Item1, nc.Item2, testSubject.IP)))
            {
                Assert.Ignore($"TestSubject: {testSubject} no matching Network-Adapter found!");
                return false;
            }
            var ping = new Ping();
            for (int i = 0; i < 5; i++)
            {
                var reply = await ping.SendPingAsync(testSubject.IP, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    Pingable = true;
                    return true;
                }
            }
            Pingable = false;
        }
        if (Pingable != true)
            Assert.Ignore($"TestSubject: {testSubject} IP not found!");
        return Pingable == true;
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        if (artNet != null)
            ((IDisposable)artNet).Dispose();
        remoteClient = null;
    }

    private void logPorts()
    {
        foreach (var port in remoteClient!.Ports.OrderBy(p => p.BindIndex).ThenBy(p => p.PortIndex))
            TestContext.Out.WriteLine($"Bind {port.BindIndex} PortIndex {port.PortIndex} Physical {port.PhysicalPort}: out {port.OutputPortAddress} in {port.InputPortAddress}, " +
                $"UIDs [{string.Join(", ", port.KnownResponderRDMUIDs.Select(b => $"{b.Uid}@{b.PortAddress}"))}]");
    }

    private async Task<UID[]> discover()
    {
        // The NETcon reports an empty ToD until its own discovery found the responders; AtcFlush starts it.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool flushed = false;
        while (sw.Elapsed < TimeSpan.FromSeconds(120))
        {
            if (!flushed && sw.Elapsed > TimeSpan.FromSeconds(3))
            {
                TestContext.Out.WriteLine($"{sw.Elapsed}: ToD still empty, sending AtcFlush");
                await instance.PerformRDMDiscovery(testSubject.RdmUniverse, flush: true);
                flushed = true;
            }
            else
                await instance.PerformRDMDiscovery(testSubject.RdmUniverse);

            for (int i = 0; i < 100; i++)
            {
                var uids = remoteClient!.Ports.SelectMany(p => p.KnownResponderRDMUIDs).Select(b => b.Uid).Distinct().ToArray();
                if (uids.Length != 0)
                {
                    TestContext.Out.WriteLine($"{sw.Elapsed}: {uids.Length} responder(s)");
                    await Task.Delay(500); // further ArtTodData of the other ports
                    return remoteClient.Ports.SelectMany(p => p.KnownResponderRDMUIDs).Select(b => b.Uid).Distinct().ToArray();
                }
                await Task.Delay(30);
            }
        }
        return [];
    }

    [Test, Order(1)]
    public void Test_Default()
    {
        Assert.That(remoteClient, Is.Not.Null);
        logPorts();
        Assert.Multiple(() =>
        {
            Assert.That(remoteClient!.LongName, Is.EqualTo(testSubject.LongName));
            Assert.That(remoteClient.IpAddress, Is.EqualTo(testSubject.IP));
            Assert.That(remoteClient.Ports, Has.Count.EqualTo(8), "2 ArtPollReplies x 4 ports");
            Assert.That(remoteClient.Ports.Count(p => p.OutputPortAddress == testSubject.RdmUniverse), Is.EqualTo(1));
        });
    }

    [Test, Order(2)]
    public async Task Test_TodLandsOnlyOnTheMatchingOutput()
    {
        Assert.That(remoteClient, Is.Not.Null);
        UID[] uids = await discover();
        logPorts();
        Assert.That(uids, Is.Not.Empty, "Responders discovered");

        var withUids = remoteClient!.Ports.Where(p => p.KnownResponderRDMUIDs.Count != 0).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(withUids, Has.Count.EqualTo(1), "The ToD belongs to one output only");
            Assert.That(withUids.FirstOrDefault()?.OutputPortAddress, Is.EqualTo(testSubject.RdmUniverse));
        });
    }

    [Test, Order(3)]
    public async Task Test_GetDeviceInfoFromEveryResponder()
    {
        Assert.That(remoteClient, Is.Not.Null);
        UID[] uids = await discover();
        Assert.That(uids, Is.Not.Empty);

        byte transaction = 100;
        foreach (UID uid in uids)
        {
            byte tn = transaction++;
            while (responses.TryDequeue(out _)) { }
            await instance.SendArtRDM(new RDMMessage
            {
                Command = ERDM_Command.GET_COMMAND,
                Parameter = ERDM_Parameter.DEVICE_INFO,
                DestUID = uid,
                TransactionCounter = tn,
            });

            ResponseRDMMessageReceivedEventArgs? response = null;
            for (int i = 0; i < 200 && response == null; i++)
            {
                response = responses.FirstOrDefault(r => r.Response.SourceUID == uid && r.Response.TransactionCounter == tn);
                await Task.Delay(10);
            }
            await Task.Delay(300); // further answers to duplicated requests
            int count = responses.Count(r => r.Response.SourceUID == uid && r.Response.TransactionCounter == tn);
            TestContext.Out.WriteLine($"{uid}: {response?.Response.ResponseType} {response?.Response.Value} ({count} response(s), PortAddress {response?.PortAddress})");

            Assert.Multiple(() =>
            {
                Assert.That(response, Is.Not.Null, $"Response from {uid}");
                Assert.That(response?.Response.Parameter, Is.EqualTo(ERDM_Parameter.DEVICE_INFO));
                Assert.That(response?.PortAddress, Is.EqualTo(testSubject.RdmUniverse));
            });
        }
    }
}
