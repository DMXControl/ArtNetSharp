using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using Microsoft.Extensions.Logging;
using RDMSharp;
using System.Diagnostics;

namespace ArtNetTests.LoopTests
{
    [Order(12)]
    public class NodeToControllerTests
    {
        private static readonly ILogger Logger = ApplicationLogging.CreateLogger<NodeToControllerTests>();
        private ArtNet artNet;
        private NodeInstanceMock instanceTX;
        private OutputPortConfig outputPort;
        private ControllerInstanceMock instanceRX;
        private InputPortConfig inputPort;

        private Task? initialTask;

        private RemoteClient? rcRX = null;
        private RemoteClient? rcTX = null;

        private static readonly PortAddress portAddress = new PortAddress(2, 3, 4);

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (ArtNetSharp.Tools.IsRunningOnGithubWorker())
                Assert.Ignore("Not running on Github-Action");

            Logger.LogDebug($"Test Setup: {nameof(NodeToControllerTests)}");

            artNet = new ArtNet();
            //artNet.LoopNetwork = new ArtNet.NetworkLoopAdapter(new IPv4Address("255.255.255.0"));

            instanceTX = new NodeInstanceMock(artNet, 0x4444);
            instanceTX.Name = $"{nameof(NodeToControllerTests)}-TX";
            instanceRX = new ControllerInstanceMock(artNet, 0x5555);
            instanceRX.Name = $"{nameof(NodeToControllerTests)}-RX";

            outputPort = new OutputPortConfig(1, portAddress);
            inputPort = new InputPortConfig(1, portAddress);

            instanceTX.AddPortConfig(inputPort);
            instanceRX.AddPortConfig(outputPort);

            byte identifyer = 192;
            if (ArtNetSharp.Tools.IsRunningOnGithubWorker())
                identifyer = 10;

            var usedNic = artNet.NetworkClients.FirstOrDefault(nc => ((IPv4Address)nc.LocalIpAddress).B1 != identifyer);
            inputPort.AddAdditionalIPEndpoints(usedNic.LocalIpAddress);
            foreach (var client in artNet.NetworkClients.Where(nc => ((IPv4Address)nc.LocalIpAddress).B1 != identifyer))
                client.Enabled = false;

            artNet.AddInstance([instanceTX, instanceRX]);
        }

        private async Task init()
        {
            DateTime startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalSeconds < 12 && (rcRX == null || rcTX == null) && !(artNet.IsDisposed || artNet.IsDisposing))
            {
                await Task.Delay(2500);
                rcRX ??= instanceTX.RemoteClients.FirstOrDefault(rc => rc.LongName.Equals(instanceRX.Name));
                rcTX ??= instanceRX.RemoteClients.FirstOrDefault(rc => rc.LongName.Equals(instanceTX.Name));
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
        public void OneTimeTearDown()
        {
            Logger.LogDebug($"Test Setup: {nameof(NodeToControllerTests)} {nameof(OneTimeTearDown)}");

            if (artNet != null)
                ((IDisposable)artNet).Dispose();

            Trace.Flush();
        }

#pragma warning disable CS0618 // Typ oder Element ist veraltet
        [Timeout(8000)]
#pragma warning restore CS0618 // Typ oder Element ist veraltet
        [Test, Order(1), Retry(5)]
        public async Task TestLoopDetection()
        {
            initialTask ??= init();
            await initialTask;
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
        [Timeout(8000)]
#pragma warning restore CS0618 // Typ oder Element ist veraltet
        [Test, Order(2)]
        public async Task TestSendDMX()
        {
            initialTask ??= init();
            await initialTask;
            Logger.LogDebug(nameof(TestSendDMX));
            Assert.Multiple(() =>
            {
                Assert.That(rcRX, Is.Not.Null);
                Assert.That(rcTX, Is.Not.Null);
            });

            byte[] data = new byte[512];
            bool receiveFlag = false;

            var txPort = rcTX.Ports.First(p => p.InputPortAddress.Equals(portAddress));
            var rxPort = rcRX.Ports.First(p => p.OutputPortAddress.Equals(portAddress));

            Assert.Multiple(() =>
            {
                Assert.That(rxPort, Is.SameAs(rcRX.Ports.First(p => p.OutputPortAddress.Equals(portAddress))));
                Assert.That(rxPort.GoodOutput.ConvertFrom, Is.EqualTo(GoodOutput.EConvertFrom.ArtNet));
                Assert.That(rxPort.GoodOutput.DMX_OutputShortCircuit, Is.False);
                Assert.That(rxPort.GoodOutput.IsBeingOutputAsDMX, Is.False);
            });

            instanceRX.DMXReceived += InstanceRX_DMXReceived;
            for (byte b = 0; b <= 25; b++)
                await doDmxStuff(b);

            instanceRX.DMXReceived -= InstanceRX_DMXReceived;

            Assert.Multiple(() =>
            {
                Assert.That(rxPort.GoodOutput.ConvertFrom, Is.EqualTo(GoodOutput.EConvertFrom.ArtNet));
                Assert.That(rxPort.GoodOutput.DMX_OutputShortCircuit, Is.False);
            });
            bool dataReceived = false;
            for (int i = 0; i < 60; i++)
            {
                dataReceived = rxPort.GoodOutput.IsBeingOutputAsDMX;
                if (dataReceived)
                    continue;
            }
            Assert.That(dataReceived, Is.True);


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
        [Timeout(9000)]
#pragma warning restore CS0618 // Typ oder Element ist veraltet
        [Test, Order(3), Retry(5)]
        public async Task TestSendDMXTiming()
        {
            initialTask ??= init();
            await initialTask;
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
            var thread = new Thread(() =>
            {
                try
                {
                    instanceRX.DMXReceived += (o, e) =>
                    {
                        receivedFlag = true;
                        swDMX.Stop();
                        if (swDMX.Elapsed.TotalMilliseconds != 0)
                        {
                            refreshRate.Add(1000.0 / swDMX.Elapsed.TotalMilliseconds);
                        }
                        swDMX.Restart();
                    };
                    instanceRX.SyncReceived += (o, e) =>
                    {
                        syncFlag = true;
                        swSync.Stop();
                        syncRate.Add(1000.0 / swSync.Elapsed.TotalMilliseconds);
                        swSync.Restart();
                    };
                    Random rnd = new Random();
                    swSync.Start();
                    swDMX.Start();
                    while ((DateTime.UtcNow - startTime).TotalSeconds <= 5)
                    {
                        rnd.NextBytes(data);
                        instanceTX.WriteDMXValues(portAddress, data); ;
                        Thread.Sleep(15);
                    }
                    swSync.Stop();
                    swDMX.Stop();
                }
                catch (Exception)
                {
                }
                finally
                {
                    done = true;
                }
            });
            thread.Priority = ThreadPriority.Normal;
            thread.Name = nameof(TestSendDMXTiming);
            thread.IsBackground = true;
            thread.Start();
            while (!done)
                await Task.Delay(100);
            Assert.Multiple(() =>
            {
                Assert.That(syncFlag, Is.True);
                Assert.That(receivedFlag, Is.True);
                Assert.That(syncRate.Average(), Is.AtLeast(40));
                Assert.That(refreshRate.Average(), Is.AtLeast(40));
            });
        }
    }
}