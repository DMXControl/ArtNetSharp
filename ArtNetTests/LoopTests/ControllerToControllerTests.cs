using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using System.Diagnostics;

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
            ArtNet.Clear();

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
            ArtNet.Clear();
            Trace.Flush();
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
            Assert.That(rxPort.GoodOutput.HasFlag(EGoodOutput.DataTransmitted), Is.False);
        }

        [Test, Order(2), Retry(3)]
        public async Task TestSendDMX()
        {
            Assert.That(rcRX, Is.Not.Null);
            Assert.That(rcTX, Is.Not.Null);
            byte[] data = new byte[512];
            bool receiveFlag = false;

            var txPort = rcTX.Ports.First(p => p.InputPortAddress.Equals(portAddress));
            var rxPort = rcRX.Ports.First(p => p.OutputPortAddress.Equals(portAddress));

            Assert.That(rxPort.GoodOutput.HasFlag(EGoodOutput.OutputArtNet), Is.True);
            Assert.That(rxPort.GoodOutput.HasFlag(EGoodOutput.DMX_OutputShortCircuit), Is.False);
            Assert.That(rxPort.GoodOutput.HasFlag(EGoodOutput.DataTransmitted), Is.False);

            instanceRX.DMXReceived += InstanceRX_DMXReceived;
            var cts=new CancellationTokenSource(100);
            for (byte b = 1; b < byte.MaxValue; b++)
            {
                var token=cts.Token;
                await Task.Run(async () => await doDmxStuff(b), token);
                if (token.IsCancellationRequested)
                    Assert.Fail(b.ToString());
            }

            instanceRX.DMXReceived -= InstanceRX_DMXReceived;

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
                receiveFlag = false;
                if (data[0] != value)
                    for (ushort i = 0; i < 512; i++)
                        data[i] = value;

                instanceTX.WriteDMXValues(portAddress, data);
                while (!receiveFlag)
                    await Task.Delay(40);
            }
            void InstanceRX_DMXReceived(object? sender, PortAddress e)
             {
                if (e != portAddress)
                    return;

                string str = $"Error at {data[0]}";
                Assert.That(outputPort.GoodOutput.HasFlag(EGoodOutput.DataTransmitted), Is.True, str);
                Assert.That(instanceRX.GetReceivedDMX(portAddress), Is.EqualTo(data), str);
                receiveFlag = true;
            }
        }
    }
}