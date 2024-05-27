//using ArtNetSharp;
//using ArtNetSharp.Communication;
//using ArtNetTests.Mocks;
//using ArtNetTests.Mocks.Instances;
//using RDMSharp;
//using RDMSharp.ParameterWrapper;
//using System.Net;

//namespace ArtNetTests
//{
//    public class NetworkTest
//    {
//        ArtNet artNet;
//        [OneTimeSetUp]
//        public void OneTimeSetUp()
//        {


//            //            Assert.Ignore("Skiped in Release!");

//            artNet = ArtNet.Instance;
//            if (artNet.Instances.Count != 0)
//                Assert.Fail();
//            //var broadcastIp = new IPAddress(new byte[] { 2, 255, 255, 255 });
//            //artNet.NetworkClients.ToList().ForEach(ncb => ncb.Enabled = IPAddress.Equals(broadcastIp, ncb.BroadcastIpAddress));
//        }


//        [Test]
//        public async Task TestNodeInstance()
//        {
//            NodeInstance instance = new NodeMock();
//            instance.Name = "Test";
//            for (ushort i = 1; i <= 32; i++)
//                instance.AddPortConfig(new PortConfig((byte)i, i, true, false) { PortNumber = (byte)i, Type = EPortType.OutputFromArtNet, GoodOutput = GoodOutput.ContiniuousOutput | GoodOutput.DataTransmitted });
//            artNet.AddInstance(instance);
//            for (int i = 0; i < 80; i++)
//                await Task.Delay(100);
//            artNet.RemoveInstance(instance);
//            ((IDisposable)instance).Dispose();
//        }

//        [Test]
//        public async Task TestControllerInstance()
//        {
//            ControllerInstanceMock instance = new ControllerInstanceMock(0x1434);
//            instance.Name = "Test";
//            for (ushort i = 1; i <= 32; i++)
//                instance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = GoodOutput.ContiniuousOutput | GoodOutput.DataTransmitted });
//            artNet.AddInstance(instance);
//            for (int i = 0; i < 80; i++)
//                await Task.Delay(100);
//            artNet.RemoveInstance(instance);
//            ((IDisposable)instance).Dispose();
//        }


//        [Test]
//        public async Task TestDMXTrafficInstance()
//        {
//            ControllerInstanceMock instance = new ControllerInstanceMock(0x7234);
//            instance.Name = "DMXTraffic Test";
//            for (ushort i = 1; i <= 4; i++)
//                instance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = GoodOutput.ContiniuousOutput | GoodOutput.DataTransmitted });
//            artNet.AddInstance(instance);

//            byte[] data = new byte[32];
//            for (int i = 0; i < 200; i++)
//            {
//                for (ushort b = 0; b < data.Length; b++)
//                    data[b] += 5;

//                await Task.Delay(10);

//                for (ushort pa = 0; pa < 2; pa++)
//                    instance.WriteDMXValues(pa, data);
//            }
//            for (int i = 0; i < 20; i++)
//            {
//                for (ushort b = 0; b < data.Length; b++)
//                    data[b] += 5;

//                await Task.Delay(100);

//                for (ushort pa = 0; pa < 2; pa++)
//                    instance.WriteDMXValues(pa, data);
//            }
//            for (int i = 0; i < 20; i++)
//                await Task.Delay(100);
//            artNet.RemoveInstance(instance);
//            ((IDisposable)instance).Dispose();
//        }

//        [Test]
//        public async Task TestRDMTrafficInstance()
//        {
//            ControllerInstanceMock instance = new ControllerInstanceMock(0x1934);
//            instance.Name = "RDMTraffic Test";
//            for (ushort i = 1; i <= 4; i++)
//                instance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = GoodOutput.ContiniuousOutput | GoodOutput.DataTransmitted });
//            artNet.AddInstance(instance);
//            for (int i = 0; i < 3; i++)
//            {
//                await Task.Delay(5000);
//                await instance.PerformRDMDiscovery();
//            }
//            await Task.Delay(5000);
//            await instance.PerformRDMDiscovery(flush: true);

//            var uids = instance.GetReceivedRDMUIDs();
//            var catalogue = RDMParameterWrapperCatalogueManager.GetInstance();
//            var supportedParameter = catalogue.ParameterWrappers.FirstOrDefault(pw => pw is SupportedParametersParameterWrapper) as SupportedParametersParameterWrapper;
//            var deviceInfoParameter = catalogue.ParameterWrappers.FirstOrDefault(pw => pw is DeviceInfoParameterWrapper) as DeviceInfoParameterWrapper;
//            Assert.That(supportedParameter, Is.Not.Null);
//            Assert.That(deviceInfoParameter, Is.Not.Null);

//            await Task.Run(async () =>
//            {
//                ERDM_Parameter[] supportedParameters = new ERDM_Parameter[0];
//                RDMDeviceInfo? deviceInfo = null;
//                foreach (var uid in uids)
//                {
//                    instance.ResponderRDMMessageReceived -= Instance_RDMMessageReceived;
//                    instance.ResponderRDMMessageReceived += Instance_RDMMessageReceived;
//                    RDMMessage message = supportedParameter.BuildGetRequestMessage();
//                    message.SourceUID = new UID(0x414c, 0);
//                    message.DestUID = uid;
//                    message.PortID_or_Responsetype = 1;
//                    await instance.SendArtRDM(message);
//                    while (supportedParameters.Length == 0)
//                        await Task.Delay(100);

//                    message = deviceInfoParameter.BuildGetRequestMessage();
//                    message.SourceUID = new UID(0x414c, 0);
//                    message.DestUID = uid;
//                    message.PortID_or_Responsetype = 1;
//                    await instance.SendArtRDM(message);

//                    while (deviceInfo == null)
//                        await Task.Delay(100);

//                }

//                void Instance_RDMMessageReceived(object? sender, RDMMessage e)
//                {
//                    switch (e.Parameter)
//                    {
//                        case ERDM_Parameter.SUPPORTED_PARAMETERS:
//                            supportedParameters = supportedParameter.GetResponseParameterDataToValue(e.ParameterData);
//                            break;
//                        case ERDM_Parameter.DEVICE_INFO:
//                            deviceInfo = deviceInfoParameter.GetResponseParameterDataToValue(e.ParameterData);
//                            break;
//                    }
//                }
//            }); ;

//            await Task.Delay(10000);
//            artNet.RemoveInstance(instance);
//            ((IDisposable)instance).Dispose();
//        }

//        [Test]
//        public async Task TestRDMDeviceInstance()
//        {
//            ControllerInstanceMock instance = new ControllerInstanceMock(0x1284);
//            instance.Name = "RDMDevice Test";
//            for (ushort i = 1; i <= 4; i++)
//                instance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = GoodOutput.ContiniuousOutput | GoodOutput.DataTransmitted });
//            artNet.AddInstance(instance);
//            for (int i = 0; i < 3; i++)
//            {
//                await Task.Delay(5000);
//                _ = instance.PerformRDMDiscovery();
//            }

//            List<RDMDeviceMock> devices = new List<RDMDeviceMock>();
//            var uids = instance.GetReceivedRDMUIDs();
//            await Task.Run(async () =>
//            {
//                foreach (var uid in uids)
//                    devices.Add(new RDMDeviceMock(uid));

//                await Task.Delay(60000);
//            });

//            await Task.Delay(60000);
//            artNet.RemoveInstance(instance);
//            ((IDisposable)instance).Dispose();
//        }


//        [Test]
//        public async Task TestSendTimeCode()
//        {
//            ControllerInstanceMock instance = new ControllerInstanceMock(0x1230);
//            instance.Name = "TestTimeCode";
//            for (ushort i = 1; i <= 32; i++)
//                instance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = GoodOutput.ContiniuousOutput | GoodOutput.DataTransmitted });
//            artNet.AddInstance(instance);
//            for (int i = 0; i < 600; i++)
//            {
//                DateTime time = DateTime.UtcNow;
//                ArtTimeCode timecode = new ArtTimeCode((byte)(time.Millisecond / 1000.0 * 30), (byte)time.Second, (byte)time.Minute, (byte)time.Hour, ETimecodeType.SMTPE);
//                await instance.SendArtTimeCode(timecode);
//                await Task.Delay(100);
//            }
//            artNet.RemoveInstance(instance);
//            ((IDisposable)instance).Dispose();
//        }

//        [Test]
//        public async Task TestSendTimeSync()
//        {
//            ControllerInstanceMock instance = new ControllerInstanceMock(0x0815);
//            instance.Name = "TestTimeSync";
//            for (ushort i = 1; i <= 32; i++)
//                instance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = GoodOutput.ContiniuousOutput | GoodOutput.DataTransmitted });
//            artNet.AddInstance(instance);
//            for (int i = 0; i < 6; i++)
//            {
//                ArtTimeSync timeSync = new ArtTimeSync(true, DateTime.UtcNow);
//                await instance.SendArtTimeSync(timeSync);
//                await Task.Delay(1000);
//            }
//            artNet.RemoveInstance(instance);
//            ((IDisposable)instance).Dispose();
//        }

//        [Test]
//        public async Task TestSendAddress()
//        {
//            ControllerInstanceMock instance = new ControllerInstanceMock(11880);
//            instance.Name = "TestAddress";
//            for (ushort i = 1; i <= 32; i++)
//                instance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet, GoodOutput = GoodOutput.ContiniuousOutput | GoodOutput.DataTransmitted });
//            artNet.AddInstance(instance);
//            for (int i = 0; i < 6; i++)
//                await Task.Delay(1000);

//            foreach (var client in instance.RemoteClients)
//                foreach (var port in client.Ports)
//                    await instance.SendArtAddress(ArtAddress.CreateSetCommand(port.BindIndex, new ArtAddressCommand(EArtAddressCommand.LedMute)), client.IpAddress);

//            await Task.Delay(10000);

//            foreach (var client in instance.RemoteClients)
//                foreach (var port in client.Ports)
//                    await instance.SendArtAddress(ArtAddress.CreateSetCommand(port.BindIndex, command: new ArtAddressCommand(EArtAddressCommand.LedNormal)), client.IpAddress);

//            for (int i = 0; i < 6; i++)
//                await Task.Delay(000);

//            artNet.RemoveInstance(instance);
//            ((IDisposable)instance).Dispose();
//        }
//    }
//}