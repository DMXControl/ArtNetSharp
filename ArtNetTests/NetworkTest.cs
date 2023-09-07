using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using RDMSharp;
using RDMSharp.ParameterWrapper;
using System.Linq;
using System.Net;

namespace ArtNetTests
{
    public class NetworkTest
    {
        ArtNet artNet;
        [SetUp]
        public void Setup()
        {
            artNet = ArtNet.Instance;
            var broadcastIp = new IPAddress(new byte[] { 2, 255, 255, 255 });
            artNet.NetworkClients.ToList().ForEach(ncb => ncb.Enabled = IPAddress.Equals(broadcastIp, ncb.BroadcastIpAddress));
        }

        [Test]
        public void TestNodeInstance()
        {
            NodeInstance instance = new NodeInstance();
            instance.Name = "Test";
            for (ushort i = 0; i < 32; i++)
                instance.AddPortConfig(new PortConfig(i, true, false) { PortNumber = (byte)i, Type = EPortType.OutputFromArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });
            artNet.AddInstance(instance);
            for (int i = 0; i < 60; i++)
                Thread.Sleep(1000);
            artNet.RemoveInstance(instance);
            instance.Dispose();
        }

        [Test]
        public void TestControllerInstance()
        {
            ControllerInstanceMock instance = new ControllerInstanceMock();
            instance.Name = "Test";
            for (ushort i = 0; i < 32; i++)
                instance.AddPortConfig(new PortConfig(i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });
            artNet.AddInstance(instance);
            for (int i = 0; i < 60; i++)
                Thread.Sleep(1000);
            artNet.RemoveInstance(instance);
            instance.Dispose();
        }


        [Test]
        public void TestDMXTrafficInstance()
        {
            ControllerInstanceMock instance = new ControllerInstanceMock();
            instance.Name = "DMXTraffic Test";
            for (ushort i = 0; i < 4; i++)
                instance.AddPortConfig(new PortConfig(i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });
            artNet.AddInstance(instance);

            byte[] data = new byte[32];
            for (int i = 0; i < 200; i++)
            {
                for (ushort b = 0; b < data.Length; b++)
                    data[b] += 5;

                Thread.Sleep(100);

                for (ushort pa = 0; pa < 2; pa++)
                    instance.WriteDMXValues(pa, data);
            }
            for (int i = 0; i < 20; i++)
            {
                for (ushort b = 0; b < data.Length; b++)
                    data[b] += 5;

                Thread.Sleep(1000);

                for (ushort pa = 0; pa < 2; pa++)
                    instance.WriteDMXValues(pa, data);
            }
            for (int i = 0; i < 20; i++)
                Thread.Sleep(1000);
            artNet.RemoveInstance(instance);
            instance.Dispose();
        }

        [Test]
        public void TestRDMTrafficInstance()
        {
            ControllerInstanceMock instance = new ControllerInstanceMock();
            instance.Name = "RDMTraffic Test";
            for (ushort i = 0; i < 4; i++)
                instance.AddPortConfig(new PortConfig(i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });
            artNet.AddInstance(instance);
            for (int i = 0; i < 3; i++)
            {
                Thread.Sleep(5000);
                _ = instance.PerformRDMDiscovery();
            }
            Thread.Sleep(5000);
            _ = instance.PerformRDMDiscovery(flush: true);

            var uids = instance.GetReceivedRDMUIDs();
            var catalogue = RDMParameterWrapperCatalogueManager.GetInstance();
            var supportedParameter = catalogue.ParameterWrappers.FirstOrDefault(pw => pw is SupportedParametersParameterWrapper) as SupportedParametersParameterWrapper;
            var deviceInfoParameter = catalogue.ParameterWrappers.FirstOrDefault(pw => pw is DeviceInfoParameterWrapper) as DeviceInfoParameterWrapper;
            Assert.That(supportedParameter, Is.Not.Null);
            Assert.That(deviceInfoParameter, Is.Not.Null);

            Task.Run(async () =>
            {
                ERDM_Parameter[] supportedParameters = new ERDM_Parameter[0];
                RDMDeviceInfo deviceInfo = null;
                foreach (var uid in uids)
                {
                    instance.RDMMessageReceived -= Instance_RDMMessageReceived;
                    instance.RDMMessageReceived += Instance_RDMMessageReceived;
                    RDMMessage message = supportedParameter.BuildGetRequestMessage();
                    message.SourceUID = new RDMUID(0x414c, 0);
                    message.DestUID = uid;
                    message.PortID_or_Responsetype = 1;
                    await instance.SendArtRDM(message);
                    while (supportedParameters.Length == 0)
                        await Task.Delay(100);

                    message = deviceInfoParameter.BuildGetRequestMessage();
                    message.SourceUID = new RDMUID(0x414c, 0);
                    message.DestUID = uid;
                    message.PortID_or_Responsetype = 1;
                    await instance.SendArtRDM(message);

                    while (deviceInfo == null)
                        await Task.Delay(100);

                }

                void Instance_RDMMessageReceived(object? sender, RDMMessage e)
                {
                    switch (e.Parameter)
                    {
                        case ERDM_Parameter.SUPPORTED_PARAMETERS:
                            supportedParameters = supportedParameter.GetResponseParameterDataToValue(e.ParameterData);
                            break;
                        case ERDM_Parameter.DEVICE_INFO:
                            deviceInfo = deviceInfoParameter.GetResponseParameterDataToValue(e.ParameterData);
                            break;
                    }
                }
            }).GetAwaiter().GetResult();

            Thread.Sleep(10000);
            artNet.RemoveInstance(instance);
            instance.Dispose();
        }

        [Test]
        public void TestRDMDeviceInstance()
        {
            ControllerInstanceMock instance = new ControllerInstanceMock();
            instance.Name = "RDMDevice Test";
            for (ushort i = 0; i < 4; i++)
                instance.AddPortConfig(new PortConfig(i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });
            artNet.AddInstance(instance);
            for (int i = 0; i < 3; i++)
            {
                Thread.Sleep(5000);
                _ = instance.PerformRDMDiscovery();
            }

            List<RDMDeviceMock> devices = new List<RDMDeviceMock>();
            var uids = instance.GetReceivedRDMUIDs();
            Task.Run(async () =>
            {
                foreach (var uid in uids)
                    devices.Add(new RDMDeviceMock(uid));

                await Task.Delay(60000);
            }).GetAwaiter().GetResult();

            Thread.Sleep(60000);
            artNet.RemoveInstance(instance);
            instance.Dispose();
        }


        [Test]
        public void TestSendTimeCode()
        {
            ControllerInstanceMock instance = new ControllerInstanceMock();
            instance.Name = "TestTimeCode";
            for (ushort i = 0; i < 32; i++)
                instance.AddPortConfig(new PortConfig(i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });
            artNet.AddInstance(instance);
            for (int i = 0; i < 600; i++)
            {
                TimeOnly time = TimeOnly.FromDateTime(DateTime.UtcNow);
                ArtTimeCode timecode = new ArtTimeCode((byte)(time.Millisecond / 1000.0 * 30), (byte)time.Second, (byte)time.Minute, (byte)time.Hour, ETimecodeType.SMTPE);
                _ = instance.SendArtTimeCode(timecode);
                Thread.Sleep(100);
            }
            artNet.RemoveInstance(instance);
            instance.Dispose();
        }

        [Test]
        public void TestSendTimeSync()
        {
            ControllerInstanceMock instance = new ControllerInstanceMock();
            instance.Name = "TestTimeSync";
            for (ushort i = 0; i < 32; i++)
                instance.AddPortConfig(new PortConfig(i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });
            artNet.AddInstance(instance);
            for (int i = 0; i < 6; i++)
            {
                ArtTimeSync timeSync = new ArtTimeSync(true, DateTime.Now);
                _ = instance.SendArtTimeSync(timeSync);
                Thread.Sleep(1000);
            }
            artNet.RemoveInstance(instance);
            instance.Dispose();
        }

        [Test]
        public void TestSendAddress()
        {
            ControllerInstanceMock instance = new ControllerInstanceMock();
            instance.Name = "TestAddress";
            for (ushort i = 0; i < 32; i++)
                instance.AddPortConfig(new PortConfig(i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });
            artNet.AddInstance(instance);
            for (int i = 0; i < 6; i++)
                Thread.Sleep(1000);

            foreach (var client in instance.RemoteClients)
                foreach (var port in client.Ports)
                    _ = instance.SendArtAddress(new ArtAddress(port.BindIndex, command: new ArtAddressCommand(EArtAddressCommand.LedMute)), client.IpAddress);

            Thread.Sleep(10000);

            foreach (var client in instance.RemoteClients)
                foreach (var port in client.Ports)
                    _ = instance.SendArtAddress(new ArtAddress(port.BindIndex, command: new ArtAddressCommand(EArtAddressCommand.LedNormal)), client.IpAddress);

            for (int i = 0; i < 6; i++)
                Thread.Sleep(1000);

            artNet.RemoveInstance(instance);
            instance.Dispose();
        }
    }
}