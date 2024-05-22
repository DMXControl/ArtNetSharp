using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using RDMSharp;
using System.Net.NetworkInformation;

namespace ArtNetTests.HardwareTests
{
    public struct Luminex_Luminode1TestSubject
    {
        public static readonly object[] TestSubjects = new object[]
        {
            //Patrick Grote, HomeLab: 04.04.2024
            new Luminex_Luminode1TestSubject() { IP = new IPv4Address("2.164.77.190"), MAC = new MACAddress("D0:69:9E:9D:4D:BE"),LongName ="LumiNode1" },
            /// For Additional Net-2s
            //new Luminex_Luminode1TestSubject() { IP = new IPv4Address("2.164.77.191"), MAC = new MACAddress("D0:69:9E:9D:4D:BE"),LongName ="LumiNode1" },
        };
        public MACAddress MAC { get; private set; }
        public IPv4Address IP { get; private set; }
        public string LongName { get; private set; }
        public override readonly string ToString()
        {
            return $"Luminex Luminode 1 ({MAC}) on IP: {IP}";
        }
    }

    [Category("Hardware"), Order(1000)]
    [TestFixtureSource(typeof(Luminex_Luminode1TestSubject), nameof(Luminex_Luminode1TestSubject.TestSubjects))]
    public class Luminex_Luminode1
    {
        private bool? Pingable;
        private ArtNet artNet;
        private readonly Luminex_Luminode1TestSubject testSubject;
        private RemoteClient? remoteClient;
        private RemoteClientPort? remoteClientPort1;
        private RemoteClientPort? remoteClientPort2;
        private ControllerInstanceMock instance;

        private static Tuple<IPv4Address, IPv4Address>[] IPs => Tools.GetIpAddresses();

        public Luminex_Luminode1(Luminex_Luminode1TestSubject _luminex_Luminode1TestSubject)
        {
            testSubject = _luminex_Luminode1TestSubject;
        }

        [OneTimeSetUp]
        public async Task SetUp()
        {
            if (!await IsPingable())
                return;

            artNet = new ArtNet();

            instance = new ControllerInstanceMock(artNet, 0x1234)
            {
                Name = $"Test: {nameof(Luminex_Luminode1)}"
            };
            for (ushort i = 1; i <= 1; i++)
                instance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = new GoodOutput(outputStyle: GoodOutput.EOutputStyle.Continuous, isBeingOutputAsDMX:true) });
            artNet.AddInstance(instance);

            for (int i = 0; i < 1000; i++)
            {
                remoteClient ??= instance.RemoteClients?.FirstOrDefault(rc => testSubject.MAC.Equals(rc.MacAddress));
                if (remoteClient != null)
                {
                    remoteClientPort1 ??= remoteClient.Ports.FirstOrDefault(p => p.BindIndex == 1);
                    remoteClientPort2 ??= remoteClient.Ports.FirstOrDefault(p => p.BindIndex == 2);
                }
                if (remoteClient != null && remoteClientPort1 != null && remoteClientPort2 != null)
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
            return false;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            if (artNet != null)
                ((IDisposable)artNet).Dispose();

            remoteClient = null;
        }

        [Test, Order(1)]
        public void Test_Default()
        {
            Assert.Multiple(() =>
            {
                Assert.That(remoteClient, Is.Not.Null);
                Assert.That(remoteClient!.LongName, Is.EqualTo(testSubject.LongName));
                Assert.That(remoteClient.IpAddress, Is.EqualTo(testSubject.IP));
                Assert.That(remoteClient.Ports, Has.Count.EqualTo(2));
                Assert.That(remoteClient.Root.Macro, Is.EqualTo(EMacroState.None));
                Assert.That(remoteClient.Root.Style, Is.EqualTo(EStCodes.StNode));
                Assert.That(remoteClient.IsSACNCapable, Is.True);
                Assert.That(remoteClient.IsLLRPCapable, Is.False);
                Assert.That(remoteClient.IsDHCPCapable, Is.False);
                Assert.That(remoteClient.Root.Status.NodeSupportSwitchingBetweenInputOutput, Is.False);

                Assert.That(remoteClientPort1, Is.Not.Null);
                Assert.That(remoteClientPort2, Is.Not.Null);
            });
        }
        [Order(2)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task Test_ArtAddressPort(byte port)
        {
            RemoteClientPort rcp = remoteClient!.Ports.FirstOrDefault(p => p.BindIndex == port)!;
            ArtPollReply backup = rcp.ArtPollReply;
            await Assert.MultipleAsync(async () => // Test Address Net;
            {
                var net = new Net(55);
                await instance.SendArtAddress(ArtAddress.CreateSetNet(rcp!.BindIndex, net), remoteClient!.IpAddress);
                for (int i = 0; i < 100; i++)
                {
                    if (rcp!.ArtPollReply.Net.Equals(net))
                        break;
                    await Task.Delay(30);
                }
                Assert.That(rcp!.ArtPollReply.Net, Is.EqualTo(net));
                await instance.SendArtAddress(ArtAddress.CreateSetNet(rcp!.BindIndex, backup!.Net), remoteClient!.IpAddress);// reset to Backup
            });
            await Assert.MultipleAsync(async () => // Test Address SubNet;
            {
                var subnet = new Subnet(11);
                await instance.SendArtAddress(ArtAddress.CreateSetSubnet(rcp!.BindIndex, subnet), remoteClient!.IpAddress);
                for (int i = 0; i < 100; i++)
                {
                    if (rcp!.ArtPollReply.Subnet.Equals(subnet))
                        break;
                    await Task.Delay(30);
                }
                Assert.That(rcp!.ArtPollReply.Subnet, Is.EqualTo(subnet));
                await instance.SendArtAddress(ArtAddress.CreateSetSubnet(rcp!.BindIndex, backup!.Subnet), remoteClient!.IpAddress);// reset to Backup
            });
            await Assert.MultipleAsync(async () => // Test Address output Universe;
            {
                var outUniverse = new Universe(11);
                var command = new ArtAddressCommand(EArtAddressCommand.DirectionTx, 0);
                await instance.SendArtAddress(ArtAddress.CreateSetOutputUniverse(rcp!.BindIndex, outUniverse, command), remoteClient!.IpAddress);
                for (int i = 0; i < 100; i++)
                {
                    if (rcp!.ArtPollReply.OutputUniverses[0].Equals(outUniverse))
                        break;
                    await Task.Delay(30);
                }
                Assert.That(rcp!.ArtPollReply.OutputUniverses[0], Is.EqualTo(outUniverse));
                Assert.That(rcp!.ArtPollReply.PortTypes[0].HasFlag(EPortType.OutputFromArtNet), Is.True);
                command = new ArtAddressCommand(backup!.PortTypes[0].HasFlag(EPortType.OutputFromArtNet) ? EArtAddressCommand.DirectionTx : EArtAddressCommand.DirectionRx, 0);
                await instance.SendArtAddress(ArtAddress.CreateSetOutputUniverse(rcp!.BindIndex, (Universe)backup?.OutputUniverses[0]!, command), remoteClient!.IpAddress);// reset to Backup
            });
            // Test Address input Universe not Supported
        }
        [Test, Order(3)]
        public async Task Test_ArtAddressIndicate()
        {
            var command = new ArtAddressCommand(EArtAddressCommand.LedMute);
            await Assert.MultipleAsync(async () => // Test Address Net;
            {
                await instance.SendArtAddress(ArtAddress.CreateSetCommand(1, command), remoteClient!.IpAddress);
                for (int i = 0; i < 100; i++)
                {
                    if (remoteClient!.Root.Status.IndicatorState != NodeStatus.EIndicatorState.Normal)
                        break;
                    await Task.Delay(30);
                }
                command = new ArtAddressCommand(EArtAddressCommand.LedNormal);
                Assert.That(remoteClient!.Root.Status.IndicatorState, Is.EqualTo(NodeStatus.EIndicatorState.Mute));
                await instance.SendArtAddress(ArtAddress.CreateSetCommand(1, command), remoteClient!.IpAddress);// reset to Backup
            });
        }
    }
}