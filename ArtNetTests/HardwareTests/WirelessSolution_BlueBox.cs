using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using RDMSharp;
using System.Net;
using System.Net.NetworkInformation;

namespace ArtNetTests.HardwareTests
{
    public struct WirelessSolutionBlueBoxTestSubject
    {
        public static readonly object[] TestSubjects = new object[]
        {
            //Patrick Grote, HomeLab: 04.04.2024
            new WirelessSolutionBlueBoxTestSubject() { IP = new IPv4Address("2.186.90.176"), MAC = new MACAddress("00:50:C2:DF:5A:B0"),LongName ="BlueBOX 1" },
            /// For Additional Net-2s
        };
        public MACAddress MAC { get; private set; }
        public IPv4Address IP { get; private set; }
        public string LongName { get; private set; }
        public override readonly string ToString()
        {
            return $"Wireless Solution BlueBox ({MAC}) on IP: {IP}";
        }
    }

    [Category("Hardware"), Order(1002)]
    [TestFixtureSource(typeof(WirelessSolutionBlueBoxTestSubject), nameof(WirelessSolutionBlueBoxTestSubject.TestSubjects))]
    public class WirelessSolution_BlueBox
    {
        private bool? Pingable;
        private ArtNet artNet;
        private readonly WirelessSolutionBlueBoxTestSubject testSubject;
        private RemoteClient? remoteClient;
        private RemoteClientPort? remoteClientPort1;
        private ControllerInstanceMock instance;
        private static Tuple<IPv4Address, IPv4Address>[] IPs => Tools.GetIpAddresses();
        public WirelessSolution_BlueBox(WirelessSolutionBlueBoxTestSubject _wirelessSolutionBlueBoxTestSubject)
        {
            testSubject = _wirelessSolutionBlueBoxTestSubject;
        }

        [OneTimeSetUp]
        public async Task SetUp()
        {
            if (!await IsPingable())
                return;

            artNet = new ArtNet();

            var broadcastIp = new IPAddress(new byte[] { 2, 255, 255, 255 });
            artNet.NetworkClients.ToList().ForEach(ncb => ncb.Enabled = IPAddress.Equals(broadcastIp, ncb.BroadcastIpAddress));

            instance = new ControllerInstanceMock(artNet, 0x3334)
            {
                Name = $"Test: {nameof(WirelessSolution_BlueBox)}"
            };
            for (ushort i = 1; i <= 1; i++)
                instance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = new GoodOutput(continiuousOutput: true, isBeingOutputAsDMX: true) });
            artNet.AddInstance(instance);

            for (int i = 0; i < 1000; i++)
            {
                remoteClient ??= instance.RemoteClients?.FirstOrDefault(rc => testSubject.MAC.Equals(rc.MacAddress));
                if (remoteClient != null)
                {
                    remoteClientPort1 ??= remoteClient.Ports.FirstOrDefault(p => p.BindIndex == 0);
                }
                if (remoteClient != null && remoteClientPort1 != null)
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
                Assert.That(remoteClient.Ports, Has.Count.EqualTo(1));
                Assert.That(remoteClient.Root.Macro, Is.EqualTo(EMacroState.None));
                Assert.That(remoteClient.Root.Style, Is.EqualTo(EStCodes.StNode));
                Assert.That(remoteClient.IsSACNCapable, Is.False);
                Assert.That(remoteClient.IsLLRPCapable, Is.False);
                Assert.That(remoteClient.IsDHCPCapable, Is.True);

                Assert.That(remoteClientPort1, Is.Not.Null);
            });
        }
    }
}