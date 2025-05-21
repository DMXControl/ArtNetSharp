using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using Microsoft.Extensions.Logging;
using org.dmxc.wkdt.Light.ArtNet;
using RDMSharp;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;

namespace ArtNetTests.LoopTests
{

    public abstract class AbstractLoopTestTestSubject
    {
        public static readonly object[] TestSubjects = getTestSubjects();
        private static object[] getTestSubjects()
        {
            Type abstractType = typeof(AbstractLoopTestTestSubject);

            // Get all types in the current assembly that inherit from the abstract class
            IEnumerable<Type> concreteTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetConstructors().Any(c => c.IsPublic && c.GetParameters().Length == 0) && abstractType.IsAssignableFrom(t));

            // Create instances of each concrete class
            List<AbstractLoopTestTestSubject> instances = new List<AbstractLoopTestTestSubject>();
            foreach (Type concreteType in concreteTypes)
            {
                if (Activator.CreateInstance(concreteType) is AbstractLoopTestTestSubject instance)
                    instances.Add(instance);
            }

            return instances.ToArray();
        }

        public override string ToString() => TestLabel;

        public readonly string TestLabel;

        public readonly InstanceTestSubject InstanceTestSubjectTX;
        public readonly InstanceTestSubject InstanceTestSubjectRX;
        public readonly PortAddress PortAddress;
        public readonly byte BindIndex;

        protected AbstractLoopTestTestSubject(string testLabel, InstanceTestSubject instanceTestSubjectTX, InstanceTestSubject instanceTestSubjectRX, PortAddress portAddress, byte bindIndex)
        {
            TestLabel = testLabel;
            InstanceTestSubjectTX = instanceTestSubjectTX;
            InstanceTestSubjectRX = instanceTestSubjectRX;
            PortAddress = portAddress;
            BindIndex = bindIndex;
        }

        public readonly struct InstanceTestSubject
        {
            public readonly Type Type;
            public readonly string Name;
            public readonly ushort ProductCode;

            public InstanceTestSubject(Type type, string name, ushort productCode)
            {
                Type = type;
                Name = name;
                ProductCode = productCode;
            }
        }
    }
    public sealed class ControllerToControllerTestSubject : AbstractLoopTestTestSubject
    {
        public ControllerToControllerTestSubject() : base(
            "ControllerToController",
            new InstanceTestSubject(typeof(ControllerInstanceMock), "Controller-TX", 0x1111),
            new InstanceTestSubject(typeof(ControllerInstanceMock), "Controller-RX", 0x2222),
            new PortAddress(2, 13, 4),
            1)
        {
        }
    }
    public sealed class ControllerToNodeTestSubject : AbstractLoopTestTestSubject
    {
        public ControllerToNodeTestSubject() : base(
            "ControllerToNode",
            new InstanceTestSubject(typeof(ControllerInstanceMock), "Controller-TX", 0x3333),
            new InstanceTestSubject(typeof(NodeInstanceMock), "Node-RX", 0x4444),
            new PortAddress(2, 15, 4),
            1)
        {
        }
    }
    public sealed class NodeToControllerTestSubject : AbstractLoopTestTestSubject
    {
        public NodeToControllerTestSubject() : base(
            "NodeToController",
            new InstanceTestSubject(typeof(NodeInstanceMock), "Node-TX", 0x5555),
            new InstanceTestSubject(typeof(ControllerInstanceMock), "Controller-RX", 0x6666),
            new PortAddress(2, 1, 4),
            1)
        {
        }
    }
    public sealed class NodeToNodeTestSubject : AbstractLoopTestTestSubject
    {
        public NodeToNodeTestSubject() : base(
            "NodeToNode",
            new InstanceTestSubject(typeof(NodeInstanceMock), "Node-TX", 0x7777),
            new InstanceTestSubject(typeof(NodeInstanceMock), "Node-RX", 0x8888),
            new PortAddress(2, 5, 4),
            1)
        {
        }
    }

    [Order(10)]
    [TestFixtureSource(typeof(AbstractLoopTestTestSubject), nameof(AbstractLoopTestTestSubject.TestSubjects))]
    public class LoopTest
    {
        private readonly AbstractLoopTestTestSubject testSubject;

        public LoopTest(AbstractLoopTestTestSubject _TestSubject)
        {
            testSubject = _TestSubject;
            Logger.LogDebug($"Initialize Test for {nameof(LoopTest)} ({testSubject.ToString()})");
        }
        private static readonly ILogger Logger = ApplicationLogging.CreateLogger<LoopTest>();
        private ArtNet artNet;
        private AbstractInstance instanceTX;
        private OutputPortConfig outputPort;
        private AbstractInstance instanceRX;
        private InputPortConfig inputPort;
        private PortAddress portAddress;

        private Task? initialTask;

        private RemoteClient? rcRX = null;
        private RemoteClient? rcTX = null;


        //[OneTimeSetUp]
        public async Task OneTimeSetUp()
        {

            Logger.LogDebug($"Test Setup: {nameof(LoopTest)}");

            artNet = new ArtNet();

            instanceTX = (AbstractInstance)Activator.CreateInstance(testSubject.InstanceTestSubjectTX.Type, artNet, testSubject.InstanceTestSubjectTX.ProductCode)!;
            instanceTX.Name = testSubject.InstanceTestSubjectTX.Name;
            instanceRX = (AbstractInstance)Activator.CreateInstance(testSubject.InstanceTestSubjectRX.Type, artNet, testSubject.InstanceTestSubjectRX.ProductCode)!;
            instanceRX.Name = testSubject.InstanceTestSubjectRX.Name;

            portAddress = testSubject.PortAddress;
            outputPort = new OutputPortConfig(testSubject.BindIndex, portAddress);
            inputPort = new InputPortConfig(testSubject.BindIndex, portAddress);

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
            int count = 0;
            while ((rcRX == null || rcTX == null) && !(artNet.IsDisposed || artNet.IsDisposing))
            {
                await Task.Delay(2500);
                count++;
                if (count > 7)
                    break;
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
        public void OneTimeTearDown()
        {
            Logger.LogDebug($"Test Setup: {nameof(LoopTest)} {nameof(OneTimeTearDown)}");

            instanceTX.RemoteClientTimedOut += InstanceTX_RemoteClientTimedOut;
            artNet.RemoveInstance(instanceTX);
            artNet.RemoveInstance(instanceRX);

            ((IDisposable)instanceTX).Dispose();
            ((IDisposable)instanceRX).Dispose();

            if (artNet != null)
                ((IDisposable)artNet).Dispose();

            Trace.Flush();
        }

#pragma warning disable CS0618 // Typ oder Element ist veraltet
        [Timeout(20000)]
#pragma warning restore CS0618 // Typ oder Element ist veraltet
        [Test, Order(1)]
        public async Task TestLoopDetection()
        {
            TestContext.Out.WriteLine($"{nameof(TestLoopDetection)} [{testSubject.TestLabel}]");
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
        [Timeout(20000)]
#pragma warning restore CS0618 // Typ oder Element ist veraltet
        [Test, Order(2)]
        public async Task TestSendDMX()
        {
            TestContext.Out.WriteLine($"{nameof(TestSendDMX)} [{testSubject.TestLabel}]");
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
        [Timeout(20000)]
#pragma warning restore CS0618 // Typ oder Element ist veraltet
        [Test, Order(3)]
        public async Task TestSendDMXTiming()
        {
            TestContext.Out.WriteLine($"{nameof(TestSendDMXTiming)} [{testSubject.TestLabel}]");
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
                swDMX.Stop();
                receivedFlag = true;
                    if (done)
                        return;
                    if (swDMX.Elapsed.TotalMilliseconds != 0)
                        refreshRate.Add(swDMX.Elapsed.TotalMilliseconds);
                };
            instanceRX.SyncReceived += async (o, e) =>
            {
                swSync.Stop();
                syncFlag = true;
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
                var sync = 1000.0 / syncRate.Average();
                var dmx = 1000.0 / refreshRate.Average();
                Logger.LogDebug($"Sync: {syncRate.Average()}ms, DMX: {refreshRate.Average()}ms, SyncRate: {sync}, DMXRate: {dmx}");
                var targetRate = 40;
                if (ArtNetSharp.Tools.IsRunningOnGithubWorker())
                    targetRate = 30;// bacuse the worker have not enougth hoursepower to run 40Hz
                Assert.Multiple(() =>
                {
                    Assert.That(syncFlag, Is.True);
                    Assert.That(receivedFlag, Is.True);
                    Assert.That(sync, Is.AtLeast(targetRate));
                    Assert.That(dmx, Is.AtLeast(targetRate));
                });
            }

            await Task.Delay(4500);
            while (!done)
                await Task.Delay(100);            
        }
    }
}