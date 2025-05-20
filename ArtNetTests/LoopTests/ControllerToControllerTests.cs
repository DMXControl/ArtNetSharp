using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using Microsoft.Extensions.Logging;
using RDMSharp;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ArtNetTests.LoopTests
{
    [Order(10)]
    public class ControllerToControllerTests
    {
        private static readonly ILogger Logger = ApplicationLogging.CreateLogger<ControllerToControllerTests>();
        private ArtNet artNet;
        private ControllerInstanceMock instanceTX;
        private OutputPortConfig outputPort;
        private ControllerInstanceMock instanceRX;
        private InputPortConfig inputPort;

        private Task? initialTask;

        private RemoteClient? rcRX = null;
        private RemoteClient? rcTX = null;

        private static readonly PortAddress portAddress = new PortAddress(2, 13, 4);

        //[OneTimeSetUp]
        public async Task OneTimeSetUp()
        {

            Logger.LogDebug($"Test Setup: {nameof(ControllerToControllerTests)}");

            artNet = new ArtNet();

            instanceTX = new ControllerInstanceMock(artNet, 0x1111);
            instanceTX.Name = $"{nameof(ControllerToControllerTests)}-TX";
            instanceRX = new ControllerInstanceMock(artNet, 0x2222);
            instanceRX.Name = $"{nameof(ControllerToControllerTests)}-RX";

            outputPort = new OutputPortConfig(1, portAddress);
            inputPort = new InputPortConfig(1, portAddress);

            instanceTX.AddPortConfig(inputPort);
            instanceRX.AddPortConfig(outputPort);

            byte identifyer = 192;
            if (ArtNetSharp.Tools.IsRunningOnGithubWorker())
                identifyer = 10;
            identifyer = 127;

            foreach (var client in artNet.NetworkClients.Where(nc => ((IPv4Address)nc.LocalIpAddress).B1 != identifyer))
                client.Enabled = false;

            instanceTX.RemoteClientTimedOut += InstanceTX_RemoteClientTimedOut;
            await Task.Delay(300);
            artNet.AddInstance([instanceTX, instanceRX]);
        }

        private void InstanceTX_RemoteClientTimedOut(object? sender, RemoteClient e)
        {
            Assert.Fail($"RemoteClient {e} timed out. Sender: {sender}");
        }

        private async Task init()
        {
            await OneTimeSetUp();
            DateTime startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalSeconds < 12 && (rcRX == null || rcTX == null) && !(artNet.IsDisposed || artNet.IsDisposing))
            {
                await Task.Delay(2500);
                rcRX ??= instanceTX.RemoteClients.FirstOrDefault(rc => rc.Root.OemCode.Equals(instanceRX.OEMProductCode) && rc.Root.ManufacturerCode == instanceRX.ESTAManufacturerCode);
                rcTX ??= instanceRX.RemoteClients.FirstOrDefault(rc => rc.Root.OemCode.Equals(instanceTX.OEMProductCode) && rc.Root.ManufacturerCode == instanceRX.ESTAManufacturerCode);
                foreach (var rc in instanceTX.RemoteClients)
                    Logger.LogTrace($"{nameof(instanceTX)} has {rc}");
                foreach (var rc in instanceRX.RemoteClients)
                    Logger.LogTrace($"{nameof(instanceRX)} has {rc}");
                Logger.LogTrace($"{nameof(rcRX)} is {rcRX}");
                Logger.LogTrace($"{nameof(rcTX)} is {rcTX}");
                if (rcRX != null && rcTX != null && rcRX.IpAddress != rcTX.IpAddress)
                    rcTX ??= instanceRX.RemoteClients.FirstOrDefault(rc => rc.IpAddress.Equals(rcRX.IpAddress));
            }
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            Logger.LogDebug($"Test Setup: {nameof(ControllerToControllerTests)} {nameof(OneTimeTearDown)}");

            instanceTX.RemoteClientTimedOut += InstanceTX_RemoteClientTimedOut;

            if (artNet != null)
                ((IDisposable)artNet).Dispose();

            Trace.Flush();
            await Task.Delay(6500);
        }

#pragma warning disable CS0618 // Typ oder Element ist veraltet
        [Timeout(60000)]
#pragma warning restore CS0618 // Typ oder Element ist veraltet
        [Test, Order(1)]
        public async Task TestLoopDetection()
        {
            initialTask ??= init();
            await initialTask;
            await Task.Delay(300);
            Logger.LogDebug(nameof(TestLoopDetection));
            Assert.Multiple(() =>
            {
                Assert.That(rcRX, Is.Not.Null);
                Assert.That(rcTX, Is.Not.Null);
            });
            Assert.Multiple(() =>
            {
                Assert.That(rcTX.Ports, Has.Count.EqualTo(1));
                Assert.That(rcRX.Ports, Has.Count.EqualTo(1));
            });

            var txPort = rcTX.Ports.First();
            var rxPort = rcRX.Ports.First();
            Assert.Multiple(() =>
            {
                Assert.That(txPort.PortType, Is.EqualTo(EPortType.InputToArtNet));
                Assert.That(rxPort.PortType, Is.EqualTo(EPortType.OutputFromArtNet));
                Assert.That(rxPort.OutputPortAddress, Is.EqualTo(portAddress));
                Assert.That(txPort.InputPortAddress, Is.EqualTo(portAddress));
                Assert.That(rxPort.GoodOutput.IsBeingOutputAsDMX, Is.False);
            });
        }

#pragma warning disable CS0618 // Typ oder Element ist veraltet
        [Timeout(60000)]
#pragma warning restore CS0618 // Typ oder Element ist veraltet
        [Test, Order(2)]
        public async Task TestSendDMX()
        {
            initialTask ??= init();
            await initialTask;
            await Task.Delay(300);
            Logger.LogDebug(nameof(TestSendDMX));
            Assert.Multiple(() =>
            {
                Assert.That(rcRX, Is.Not.Null);
                Assert.That(rcTX, Is.Not.Null);
            });

            byte[] data = new byte[512];
            bool receiveFlag = false;

            Assert.That(rcRX.Ports, Has.Count.EqualTo(1), () =>
            {
                var portList = string.Join(", ", rcRX.Ports.Select(p => $"{p.ToString()} [{p.PortType} on PortAddress {p.OutputPortAddress}]"));
                return $"rcRX.Ports.Count != 2. Aktuelle Ports: [{portList}]";
            });
            var rxPort = rcRX.Ports.First(p => p.OutputPortAddress.Equals(portAddress));

            Assert.Multiple(() =>
            {
                Assert.That(rxPort.GoodOutput.ConvertFrom, Is.EqualTo(GoodOutput.EConvertFrom.ArtNet));
                Assert.That(rxPort.GoodOutput.DMX_OutputShortCircuit, Is.False);
                Assert.That(rxPort.GoodOutput.IsBeingOutputAsDMX, Is.False);
            });

            instanceRX.DMXReceived += InstanceRX_DMXReceived;
            for (byte b = 0; b <= 10; b++)
                await doDmxStuff(b);

            instanceRX.DMXReceived -= InstanceRX_DMXReceived;

            Assert.Multiple(() =>
            {
                Assert.That(rxPort.GoodOutput.ConvertFrom, Is.EqualTo(GoodOutput.EConvertFrom.ArtNet));
                Assert.That(rxPort.GoodOutput.DMX_OutputShortCircuit, Is.False);
            });


            async Task doDmxStuff(byte value)
            {
                if (data[0] != value)
                    for (ushort i = 0; i < 512; i++)
                        data[i] = value;

                receiveFlag = false;
                instanceTX.WriteDMXValues(portAddress, data);
                while (!receiveFlag)
                    await Task.Delay(30);

                string str = $"Error at {data[0]}";
                Assert.Multiple(() =>
                {
                    Assert.That(rxPort, Is.SameAs(rcRX.Ports.First(p => p.OutputPortAddress.Equals(portAddress))));
                    Assert.That(rxPort.GoodOutput.IsBeingOutputAsDMX, Is.True, str);
                    Assert.That(instanceRX.GetReceivedDMX(portAddress), Is.EqualTo(data), str);
                });
                await Task.Delay(100);
            }
            void InstanceRX_DMXReceived(object? sender, PortAddress e)
            {
                if (e != portAddress)
                    return;

                Assert.That(e, Is.EqualTo(portAddress));
                Task.Run(async () =>
                {
                    while (rxPort.GoodOutput.IsBeingOutputAsDMX == false)
                    {
                        Assert.That(rxPort, Is.SameAs(rcRX.Ports.First(p => p.OutputPortAddress.Equals(portAddress))));
                        await Task.Delay(10); // wait for ArtPoll and ArtPollReply to update the data of ArtPollReply-Cache
                    }
                    receiveFlag = true;
                }, new CancellationTokenSource(3500).Token);
            }
        }

#pragma warning disable CS0618 // Typ oder Element ist veraltet
        [Timeout(60000)]
#pragma warning restore CS0618 // Typ oder Element ist veraltet
        [Test, Order(3)]
        public async Task TestSendDMXTiming()
        {
            initialTask ??= init();
            await initialTask;
            await Task.Delay(300);
            Logger.LogDebug(nameof(TestSendDMXTiming));
            //if(ArtNetSharp.Tools.IsRunningOnGithubWorker())
            //    Assert.Ignore("Skiped, only run on Linux");

            DateTime startTime = DateTime.UtcNow;
            Assert.Multiple(() =>
            {
                Assert.That(rcRX, Is.Not.Null);
                Assert.That(rcTX, Is.Not.Null);
            });
            Stopwatch swDMX = new Stopwatch();
            Stopwatch swSync = new Stopwatch();
            List<double> refreshRate = new List<double>();
            List<double> syncRate = new List<double>();
            byte[] data = new byte[100];
            bool receivedFlag = false;
            bool syncFlag = false;
            bool done = false;

            swSync.Start();
            swDMX.Start();
            swSync.Stop();
            swDMX.Stop();

            Random rnd = new Random();
            instanceRX.DMXReceived += (o, e) =>
                {
                    receivedFlag = true;
                    swDMX.Stop();
                    if (done)
                        return;
                    if (swDMX.Elapsed.TotalMilliseconds != 0)
                        refreshRate.Add(swDMX.Elapsed.TotalMilliseconds);
                };
            instanceRX.SyncReceived += async (o, e) =>
            {
                syncFlag = true;
                swSync.Stop();
                if (done)
                    return;
                syncRate.Add(swSync.Elapsed.TotalMilliseconds);
                await nextData();
            };
             _ = nextData();
            async Task nextData()
            {
                await Task.Delay(5);
                if ((DateTime.UtcNow - startTime).TotalSeconds >= 5)
                {
                    done = true;
                    check();
                    return;
                }

                rnd.NextBytes(data);
                instanceTX.WriteDMXValues(portAddress, data);
                swDMX.Restart();
                swSync.Restart();
                
            }
            void check()
            {
                Assert.Multiple(() =>
                {
                    Assert.That(syncFlag, Is.True);
                    Assert.That(receivedFlag, Is.True);
                    Assert.That(1000.0 / syncRate.Average(), Is.AtLeast(40));
                    Assert.That(1000.0 / refreshRate.Average(), Is.AtLeast(40));
                });
            }

            await Task.Delay(4500);
            while (!done)
                await Task.Delay(100);            
        }
    }
}