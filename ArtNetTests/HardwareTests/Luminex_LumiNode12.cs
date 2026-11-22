using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetSharp.Misc;
using ArtNetTests.Mocks;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace ArtNetTests.HardwareTests;

public struct Luminex_LumiNode12TestSubject
{
    public static readonly object[] TestSubjects = new object[]
    {
        //Patrick Grote, HomeLab: 22.11.2026, FW 2.9.1
        new Luminex_LumiNode12TestSubject()
        {
            IP = new IPv4Address("2.164.93.125"),
            MAC = new MACAddress("d0:69:9e:a0:5d:7d"),
            LongName = "LumiNode12",
            Responders = new Dictionary<ushort, UID[]>
            {
                [0] = [new UID(0x5377, 0x000B180F)],                                                       // Swisson XPD-28
                [1] = [new UID(0x5377, 0x000B1810)],                                                       // Swisson XPD-28
                [2] = [new UID(0x4C4C, 0x4000068F)],                                                       // Luminex LumiSplit 1.6
                [3] = [new UID(0x676C, 0x000FFFFF), new UID(0x676C, 0x000FFFFC), new UID(0x676C, 0x000FFFFD)], // 3x GLP FR-1
                [4] = [new UID(0x5347, 0xA5D3AC25), new UID(0x5347, 0x9471AF2F), new UID(0x5347, 0x166199F5)], // SGM Q-2, I-2, P-2
                [5] = [],
                [6] = [],
                [7] = [],
                [8] = [],
                [9] = [new UID(0x4C55, 0x0000D5AA)],                                                       // LumenRadio CRMX Nova FX
                [10] = [new UID(0x5753, 0x6E923091)],                                                      // Wireless Solution W-DMX G4S
                [11] = [new UID(0x4658, 0x00500616)],                                                      // Magic FX FX-SWITCHPACK
            }
        },
    };
    public MACAddress MAC { get; private set; }
    public IPv4Address IP { get; private set; }
    public string LongName { get; private set; }
    /// <summary>Expected RDM responders per output Port-Address (Net 0, Sub-Net 0, Universe = key).</summary>
    public Dictionary<ushort, UID[]> Responders { get; private set; }
    public override readonly string ToString()
    {
        return $"Luminex LumiNode12 ({MAC}) on IP: {IP}";
    }
}

/// <summary>
/// LumiNode12 FW 2.9.1: 16 ArtPollReplies with one port each (BindIndex 1-12: DMX outputs on Universe 0-11,
/// BindIndex 13-16: process blocks without a physical port). 13 RDM responders on 8 outputs, up to 3 per output.
/// </summary>
[Category("Hardware")]
[DependsOnFixture(typeof(TestOS))]
[TestFixtureSource(typeof(Luminex_LumiNode12TestSubject), nameof(Luminex_LumiNode12TestSubject.TestSubjects))]
public class Luminex_LumiNode12
{
    private bool? Pingable;
    private ArtNet artNet;
    private readonly Luminex_LumiNode12TestSubject testSubject;
    private RemoteClient? remoteClient;
    private ControllerInstanceMock instance;
    private readonly ConcurrentQueue<ResponseRDMMessageReceivedEventArgs> responses = new();
    private int uidReceivedEvents;

    private static Tuple<IPv4Address, IPv4Address>[] IPs => Tools.GetIpAddresses();
    private UID[] allResponders => testSubject.Responders.Values.SelectMany(u => u).ToArray();

    public Luminex_LumiNode12(Luminex_LumiNode12TestSubject testSubject)
    {
        this.testSubject = testSubject;
    }

    [OneTimeSetUp]
    public async Task SetUp()
    {
        if (!await IsPingable())
            return;

        artNet = new ArtNet();
        instance = new ControllerInstanceMock(artNet, 0x1236) { Name = $"Test: {nameof(Luminex_LumiNode12)}" };
        instance.AddPortConfig(new PortConfig(1, new PortAddress(0, 0, 0), false, true) { PortNumber = 1, Type = EPortType.InputToArtNet | EPortType.ArtNet });
        instance.ResponseRDMMessageReceived += (_, e) => responses.Enqueue(e);
        artNet.AddInstance(instance);

        for (int i = 0; i < 1000; i++)
        {
            remoteClient ??= instance.RemoteClients?.FirstOrDefault(rc => testSubject.MAC.Equals(rc.MacAddress));
            if (remoteClient?.Ports.Count >= 16)
                break;
            await Task.Delay(10);
        }
        if (remoteClient != null)
            remoteClient.RDMUIDReceived += (_, _) => Interlocked.Increment(ref uidReceivedEvents);
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
        foreach (var port in remoteClient!.Ports.OrderBy(p => p.BindIndex))
            TestContext.Out.WriteLine($"Bind {port.BindIndex,2}: out {port.OutputPortAddress} RDM {port.IsRDMCapable}, " +
                $"UIDs [{string.Join(", ", port.KnownResponderRDMUIDs.Select(b => $"{b.Uid}@{b.PortAddress}"))}]");
    }

    private static async Task<bool> within(Func<bool> condition, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(20);
        return condition();
    }

    /// <summary>ToD of every output until all expected responders are known.</summary>
    private async Task<bool> discoverAll()
    {
        bool allKnown() => allResponders.All(uid => remoteClient!.Ports.Any(p => p.KnownResponderRDMUIDs.Any(b => b.Uid == uid)));
        for (int i = 0; i < 20 && !allKnown(); i++)
        {
            await instance.PerformRDMDiscovery();
            await within(allKnown, 1000);
        }
        return allKnown();
    }

    [Test]
    public void Test_Default()
    {
        Assert.That(remoteClient, Is.Not.Null);
        logPorts();
        Assert.Multiple(() =>
        {
            Assert.That(remoteClient!.LongName, Is.EqualTo(testSubject.LongName));
            Assert.That(remoteClient.IpAddress, Is.EqualTo(testSubject.IP));
            Assert.That(remoteClient.Root.Style, Is.EqualTo(EStCodes.StNode));
            Assert.That(remoteClient.Ports, Has.Count.EqualTo(16), "One ArtPollReply per process block");
            Assert.That(remoteClient.Ports.Select(p => p.BindIndex), Is.EquivalentTo(Enumerable.Range(1, 16).Select(i => (byte)i)));
            for (byte bind = 1; bind <= 12; bind++)
                Assert.That(remoteClient.Ports.Single(p => p.BindIndex == bind).OutputPortAddress,
                    Is.EqualTo(new PortAddress(0, 0, (byte)(bind - 1))), $"BindIndex {bind}");
        });
    }

    [Test, DependsOnTest(nameof(Test_Default))]
    public async Task Test_TodLandsOnItsOutput()
    {
        Assert.That(remoteClient, Is.Not.Null);
        bool found = await discoverAll();
        logPorts();
        Assert.That(found, Is.True, "All expected responders discovered");

        Assert.Multiple(() =>
        {
            foreach (var (universe, uids) in testSubject.Responders)
            {
                var output = new PortAddress(0, 0, (byte)universe);
                var port = remoteClient!.Ports.Single(p => p.BindIndex == universe + 1);
                Assert.That(port.KnownResponderRDMUIDs.Select(b => b.Uid), Is.EquivalentTo(uids), $"Output {output}");
            }
            foreach (var port in remoteClient!.Ports.Where(p => p.BindIndex > 12))
                Assert.That(port.KnownResponderRDMUIDs, Is.Empty, $"BindIndex {port.BindIndex} has no physical port");
        });
    }

    [Test, DependsOnTest(nameof(Test_DeviceInfoFromAllRespondersAtOnce))] // after it: RDM on an output is slow while the node rediscovers
    public async Task Test_FlushDoesNotRemoveResponders()
    {
        Assert.That(remoteClient, Is.Not.Null);
        Assert.That(await discoverAll(), Is.True);
        int before = Volatile.Read(ref uidReceivedEvents);

        foreach (var universe in testSubject.Responders.Keys)
            await instance.PerformRDMDiscovery(new PortAddress(0, 0, (byte)universe), flush: true);

        // the node rediscovers and answers ArtTodRequests meanwhile; responders must stay known throughout
        var sw = Stopwatch.StartNew();
        int minimum = int.MaxValue;
        while (sw.Elapsed < TimeSpan.FromSeconds(8))
        {
            await instance.PerformRDMDiscovery();
            await Task.Delay(500);
            minimum = Math.Min(minimum, remoteClient!.Ports.Sum(p => p.KnownResponderRDMUIDs.Count));
        }
        logPorts();
        Assert.Multiple(() =>
        {
            Assert.That(minimum, Is.EqualTo(allResponders.Length), "No responder was removed while the node rediscovered");
            Assert.That(Volatile.Read(ref uidReceivedEvents) - before, Is.EqualTo(0), "No responder was announced again");
        });
    }

    [Test, DependsOnTest(nameof(Test_Default))]
    public async Task Test_DeviceInfoFromAllRespondersAtOnce()
    {
        Assert.That(remoteClient, Is.Not.Null);
        Assert.That(await discoverAll(), Is.True);
        responses.Clear();

        var requests = testSubject.Responders
            .SelectMany(kv => kv.Value.Select(uid => (universe: kv.Key, uid)))
            .Select((r, i) => (r.universe, r.uid, tn: (byte)(150 + i)))
            .ToArray();
        await Task.WhenAll(requests.Select(r => instance.SendArtRDM(new RDMMessage
        {
            Command = ERDM_Command.GET_COMMAND,
            Parameter = ERDM_Parameter.DEVICE_INFO,
            DestUID = r.uid,
            TransactionCounter = r.tn,
        })));

        bool answered((ushort universe, UID uid, byte tn) r) => responses.Any(e => e.Response.SourceUID == r.uid && e.Response.TransactionCounter == r.tn);
        // RDM responses can take seconds (seen: 3x GLP FR-1 on one output answered after 6.6 s while the node was discovering)
        await within(() => requests.All(answered), 15000);
        await Task.Delay(300); // answers to duplicated requests would arrive now

        Assert.Multiple(() =>
        {
            foreach (var r in requests)
            {
                var matching = responses.Where(e => e.Response.SourceUID == r.uid && e.Response.TransactionCounter == r.tn).ToList();
                TestContext.Out.WriteLine($"{r.uid}: {string.Join(", ", matching.Select(m => $"{m.Response.ResponseType} @{m.PortAddress}"))}");
                Assert.That(matching, Has.Count.EqualTo(1), $"{r.uid}: exactly one response");
                if (matching.Count != 0)
                {
                    Assert.That(matching[0].Response.Parameter, Is.EqualTo(ERDM_Parameter.DEVICE_INFO));
                    Assert.That(matching[0].PortAddress, Is.EqualTo(new PortAddress(0, 0, (byte)r.universe)), $"{r.uid}: answered on its output");
                }
            }
        });
    }
}
