using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;

namespace ArtNetTests.LoopTests
{
    public class ControllerToControllerTests
    {
        private ArtNet artNet;
        private ControllerInstanceMock instanceTX;
        private OutputPortConfig outputPort;
        private ControllerInstanceMock instanceRX;
        private InputPortConfig inputPort;

        private RemoteClient? rcRX = null;
        private RemoteClient? rcTX = null;

        private static readonly PortAddress portAddress = new PortAddress(2, 3, 4);

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            artNet = ArtNet.Instance;
            foreach(var inst in artNet.Instances)
                artNet.RemoveInstance(inst);
            instanceTX = new ControllerInstanceMock(0x1111);
            instanceTX.Name = $"{nameof(ControllerToControllerTests)}-TX";
            instanceRX = new ControllerInstanceMock(0x2222);
            instanceRX.Name = $"{nameof(ControllerToControllerTests)}-RX";

            outputPort = new OutputPortConfig(1, portAddress);
            inputPort = new InputPortConfig(1, portAddress);

            instanceTX.AddPortConfig(inputPort);
            instanceRX.AddPortConfig(outputPort);

            artNet.AddInstance([instanceTX, instanceRX]);
            CancellationTokenSource cts=new CancellationTokenSource(30000);
            var task = Task.Run(async () =>
            {
                while (rcRX == null || rcTX == null)
                {
                    await Task.Delay(10);
                    if (rcRX == null)
                        rcRX = instanceTX.RemoteClients.FirstOrDefault(rc => rc.LongName.Equals(instanceRX.Name));
                    if (rcTX == null)
                        rcTX = rcTX ?? instanceRX.RemoteClients.FirstOrDefault(rc => rc.LongName.Equals(instanceTX.Name));
                }
            }, cts.Token);
            while (!cts.IsCancellationRequested && (rcRX == null || rcTX ==null))
                Thread.Sleep(30);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            artNet.RemoveInstance([instanceTX, instanceRX]);
            ((IDisposable)instanceTX).Dispose();
            ((IDisposable)instanceRX).Dispose();
        }

        [Test, Order(1)]
        public void TestLoopDetection()
        {
            Assert.That(rcRX, Is.Not.Null);
            Assert.That(rcTX, Is.Not.Null);
            Assert.That(rcTX.Ports, Has.Count.EqualTo(1));
            Assert.That(rcRX.Ports, Has.Count.EqualTo(1));

            var txPort = rcTX.Ports.First();
            var rxPort = rcRX.Ports.First();
            Assert.That(txPort.PortType, Is.EqualTo(EPortType.InputToArtNet));
            Assert.That(rxPort.PortType, Is.EqualTo(EPortType.OutputFromArtNet));
            Assert.That(rxPort.OutputPortAddress, Is.EqualTo(portAddress));
            Assert.That(txPort.InputPortAddress, Is.EqualTo(portAddress));
        }

        [Test, Order(2), Retry(3)]
        public async Task TestSendDMX()
        {
            Assert.That(rcRX, Is.Not.Null);
            Assert.That(rcTX, Is.Not.Null);
            byte[] data = new byte[512];

            var txPort = rcTX.Ports.First(p => p.InputPortAddress.Equals(portAddress));
            var rxPort = rcRX.Ports.First(p => p.OutputPortAddress.Equals(portAddress));

            Assert.That(rxPort.GoodOutput.HasFlag(EGoodOutput.OutputArtNet), Is.True);
            Assert.That(rxPort.GoodOutput.HasFlag(EGoodOutput.DMX_OutputShortCircuit), Is.False);
            Assert.That(rxPort.GoodOutput.HasFlag(EGoodOutput.DataTransmitted), Is.False);

            for (byte b = 0; b < byte.MaxValue; b++)
                await doDmxStuff(b);

            Assert.That(rxPort.GoodOutput.HasFlag(EGoodOutput.OutputArtNet), Is.True);
            Assert.That(rxPort.GoodOutput.HasFlag(EGoodOutput.DMX_OutputShortCircuit), Is.False);
            bool dataReceived = false;
            for (int i = 0; i < 60; i++)
            {
                dataReceived = rxPort.GoodOutput.HasFlag(EGoodOutput.DataTransmitted);
                if (dataReceived)
                    continue;
            }
            Assert.That(dataReceived, Is.True);

            async Task doDmxStuff(byte value)
            {
                if (data[0] != value)
                    for (ushort i = 0; i < 512; i++)
                        data[i] = value;

                instanceTX.WriteDMXValues(portAddress, data);
                await Task.Delay(70);
                Assert.That(outputPort.GoodOutput.HasFlag(EGoodOutput.DataTransmitted), Is.True);
                Assert.That(instanceRX.GetReceivedDMX(portAddress), Is.EqualTo(data));
            }
        }
    }
}