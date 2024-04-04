using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using RDMSharp;
using System.Net;
using System.Net.NetworkInformation;

namespace ArtNetTests.HardwareTests
{
    public struct ShowtecNet2TestSubject
    {
        public static readonly object[] TestSubjects = new object[]
        {
            //Patrick Grote, HomeLab: 04.04.2024
            new ShowtecNet2TestSubject() { IP = new IPv4Address("2.1.1.1"), MAC = new MACAddress("42:4C:4B:64:3F:86"),LongName ="NetWork Node 2(42:4C:4B:64:3F:86)" },
            /// For Additional Net-2s
            //new ShowtecNet2TestSubject() { IP = new IPv4Address("2.1.1.2"), MAC = new MACAddress("42:4C:4B:64:3F:86"),LongName ="NetWork Node 2(42:4C:4B:64:3F:86)" }
        };
        public MACAddress MAC { get; private set; }
        public IPv4Address IP { get; private set; }
        public string LongName { get; private set; }
        public override string ToString()
        {
            return $"Showtec Net-2 ({MAC}) on IP: {IP}";
        }
    }

    [Category("Hardware")]
    [TestFixtureSource(typeof(ShowtecNet2TestSubject), nameof(ShowtecNet2TestSubject.TestSubjects))]
    public class Showtec_Net2
    {
        private bool? Pingable;
        private static readonly ArtNet artNet = ArtNet.Instance;
        private readonly ShowtecNet2TestSubject testSubject;
        private RemoteClient? remoteClient;
        private RemoteClientPort? remoteClientPort1;
        private RemoteClientPort? remoteClientPort2;
        private ControllerInstanceMock instance;
        public Showtec_Net2(ShowtecNet2TestSubject _showtecNet2TestSubject)
        {
            testSubject = _showtecNet2TestSubject; 
        }

        [OneTimeSetUp]
        public async Task SetUp()
        {
            if (!await IsPingable())
                return;

            instance = new ControllerInstanceMock
            {
                Name = $"Test: {nameof(Showtec_Net2)}"
            };
            for (ushort i = 1; i <= 1; i++)
                instance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });
            artNet.AddInstance(instance);

            for (int i = 0; i < 1000; i++)
            {
                if (remoteClient == null)
                    remoteClient = instance.RemoteClients?.FirstOrDefault(rc => testSubject.MAC.Equals(rc.MacAddress));
                if (remoteClient != null)
                {
                    if (remoteClientPort1 == null)
                        remoteClientPort1 = remoteClient.Ports.FirstOrDefault(p => p.BindIndex == 1);
                    if (remoteClientPort2 == null)
                        remoteClientPort2 = remoteClient.Ports.FirstOrDefault(p => p.BindIndex == 2);
                }
                if (remoteClient != null && remoteClientPort1 != null && remoteClientPort2 != null)
                    return;

                await Task.Delay(10);
            }
        }

        private async Task<bool> IsPingable()
        {
            if (!Pingable.HasValue)
            {
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
                Assert.Ignore($"TestSubject:{testSubject} IP not found!");
            return false;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            if (instance != null)
            {
                artNet?.RemoveInstance(instance);
                ((IDisposable)instance).Dispose();
            }
                
            remoteClient = null;
        }

        [Test, Order(1)]
        public async Task Test_Default()
        {
            Assert.That(remoteClient, Is.Not.Null);
            Assert.That(remoteClient.LongName, Is.EqualTo(testSubject.LongName));
            Assert.That(remoteClient.IpAddress, Is.EqualTo(testSubject.IP));
            if (remoteClient.Ports.Count == 1)
                await Task.Delay(1000); //Give the Net-2 more time to send the 2nd Port
            Assert.That(remoteClient.Ports, Has.Count.EqualTo(2));
            Assert.That(remoteClient.Root.Macro, Is.EqualTo(EMacroState.None));
            Assert.That(remoteClient.Root.Style, Is.EqualTo(EStCodes.StNode));
            Assert.That(remoteClient.IsSACNCapable, Is.True);
            Assert.That(remoteClient.IsLLRPCapable, Is.False);
            Assert.That(remoteClient.IsDHCPCapable, Is.True);

            Assert.That(remoteClientPort1, Is.Not.Null);
            Assert.That(remoteClientPort2, Is.Not.Null);

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
                for (int i = 0; i < 10; i++)
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
                for (int i = 0; i < 10; i++)
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
                for (int i = 0; i < 10; i++)
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
            await Assert.MultipleAsync(async () => // Test Address input Universe;
            {
                var inUniverse = new Universe(7);
                var command = new ArtAddressCommand(EArtAddressCommand.DirectionRx, 0);
                await instance.SendArtAddress(ArtAddress.CreateSetInputUniverse(rcp!.BindIndex, inUniverse, command), remoteClient!.IpAddress);
                for (int i = 0; i < 10; i++)
                {
                    if (rcp!.ArtPollReply.InputUniverses[0].Equals(inUniverse))
                        break;
                    await Task.Delay(30);
                }
                Assert.That(rcp!.ArtPollReply.InputUniverses[0], Is.EqualTo(inUniverse));
                Assert.That(rcp!.ArtPollReply.PortTypes[0].HasFlag(EPortType.InputToArtNet), Is.True);
                command = new ArtAddressCommand(backup!.PortTypes[0].HasFlag(EPortType.OutputFromArtNet) ? EArtAddressCommand.DirectionTx : EArtAddressCommand.DirectionRx, 0);
                await instance.SendArtAddress(ArtAddress.CreateSetInputUniverse(rcp!.BindIndex, (Universe)backup?.InputUniverses[0]!, command), remoteClient!.IpAddress);// reset to Backup
            });
        }
        [Test, Order(3)]
        public async Task Test_ArtAddressIndicate()
        {
            var command = new ArtAddressCommand(EArtAddressCommand.LedLocate);
            await Assert.MultipleAsync(async () => // Test Address Net;
            {
                await instance.SendArtAddress(ArtAddress.CreateSetCommand(1, command), remoteClient!.IpAddress);
                for (int i = 0; i < 10; i++)
                {
                    if (!remoteClient!.Root.Status.HasFlag(ENodeStatus.IndicatorStateNormal))
                        break;
                    await Task.Delay(30);
                }
                command = new ArtAddressCommand(EArtAddressCommand.LedNormal);
                Assert.That(remoteClientPort2!.ArtPollReply.Status.HasFlag(ENodeStatus.IndicatorStateNormal), Is.False);
                await instance.SendArtAddress(ArtAddress.CreateSetCommand(1, command), remoteClient!.IpAddress);// reset to Backup
            });
        }
    }
}