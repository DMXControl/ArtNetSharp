using ArtNetSharp;
using Tools = ArtNetSharp.Tools;
using RDMSharp;
using RDMTools = RDMSharp.Tools;
using ArtNetSharp.Messages.Interfaces;

namespace ArtNetTests
{
    public class PackagesSerializeDeserialize
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
        }


        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Tools_TryDeserializePacket()
        {
            byte[]? data = null;
            Assert.That(Tools.TryDeserializePacket(data, out _), Is.False);

            data = [];
            Assert.That(Tools.TryDeserializePacket(data, out _), Is.False);
            data = [0x00, 0x01];
            Assert.That(Tools.TryDeserializePacket(data, out _), Is.False);

            data = new byte[12];
            Assert.That(Tools.TryDeserializePacket(data, out _), Is.False);

            ArtTodControl src = new ArtTodControl(1, new Address(3, 4));
            data = src;
            data = data.Take(22).ToArray();
            Assert.That(Tools.TryDeserializePacket(data, out _), Is.False);
        }

        [Test]
        public void ArtSync()
        {
            ArtSync src = new ArtSync();
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtSync des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtPoll()
        {
            ArtPoll src = new ArtPoll(0x1001,
                                      0xea0f,
                                      0x0001,
                                      0x0f00,
                                      EArtPollFlags.DiagnosticEnabled | EArtPollFlags.DiagnosticUnicast,
                                      EPriorityCode.DpMed);
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            doImplicidTests(src);
            if (res is ArtPoll des)
                Assert.That(des, Is.EqualTo(src));

            src = new ArtPoll(oemCode: 0x1001,
                              manufacturerCode: 0xea0f,
                              flags:
                              EArtPollFlags.DiagnosticEnabled | EArtPollFlags.DiagnosticUnicast,
                              priority: EPriorityCode.DpMed);
            array = src;
            Assert.That(Tools.TryDeserializePacket(array, out res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            doImplicidTests(src);
            if (res is ArtPoll des2)
                Assert.That(des2, Is.EqualTo(src));

        }
        [Test]
        public void ArtPollReply()
        {
            ArtPollReply src = new ArtPollReply(new IPv4Address("2.3.2.3"),
                                                new IPv4Address("4.4.4.4"),
                                                new MACAddress(2, 44, 5, 77, 8, 34),
                                                "UnitTest",
                                                "This is a UnitTest for Testing this Packet.",
                                                3,
                                                new NodeStatus(
                                                    dHCP_ConfigurationSupported: true,
                                                    dHCP_ConfigurationUsed: true,
                                                    indicatorState: NodeStatus.EIndicatorState.Locate,
                                                    portAddressBitResolution: NodeStatus.EPortAddressBitResolution._15Bit,
                                                    rDM_Supported: true),


                                                3,
                                                6,
                                                new Net(8),
                                                new Subnet(14),
                                                new object[] { new Universe(12) },
                                                new object[] { new Universe(9) },
                                                0x0789,
                                                0x2345,
                                                1,
                                                new NodeReport(ENodeReportCodes.RcFactoryRes, "ewqewfffafdafafgg", 55),
                                                new EPortType[] { EPortType.DMX512
                                                                | EPortType.OutputFromArtNet },
                                                new EGoodInput[] { EGoodInput.None },
                                                new EGoodOutput[] { EGoodOutput.ContiniuousOutput
                                                                 | EGoodOutput.RDMisDisabled
                                                                 | EGoodOutput.DataTransmitted
                                                                 | EGoodOutput.DMX_OutputShortCircuit },
                                                EMacroState.Macro1Active
                                                    | EMacroState.Macro2Active,
                                                ERemoteState.Remote3Active
                                                    | ERemoteState.Remote4Active,
                                                44,
                                                77,
                                                4,
                                                44,
                                                EStCodes.StConfig,
                                                new RDMUID(0x1122, 0x33445566));
                

            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtPollReply des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtDMX()
        {
            ArtDMX src = new ArtDMX(34, 0, 2, new Address(3, 4), new byte[] { 1, 2, 3, 44, 55, 66, 88, 222, 111, 0x33 });
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtDMX des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtNzs()
        {
            ArtNzs src = new ArtNzs(53, 0xED, 2, new Address(3, 4), new byte[] { 1, 2, 3, 44, 55, 66, 88, 222, 111, 0x33 });
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtNzs des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtVlc()
        {
            ArtVlc src = new ArtVlc(59,
                                    9,
                                    new Address(5, 1),
                                    new byte[] { 1, 2, 3, 44, 55, 66, 88, 222, 111, 0x33 },
                                    0xeacb,
                                    0xabcd,
                                    3,
                                    5,
                                    435,
                                    1122,
                                    666,
                                    EVlcFlags.Beacon | EVlcFlags.IEEE | EVlcFlags.Reply,
                                    EPayloadLanguageCode.BeaconText);
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtVlc des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtInput()
        {
            ArtInput src = new ArtInput(33, [EArtInputCommand.DisableInput, EArtInputCommand.DisableInput, EArtInputCommand.None], 3, 0, 23);
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtInput des)
                Assert.That(des, Is.EqualTo(src));

            Assert.Throws(typeof(ArgumentOutOfRangeException), () => { var artInput = new ArtInput(33, [], 5, 0, 23); });
            Assert.Throws(typeof(ArgumentOutOfRangeException), () => { var artInput = new ArtInput(35, [], 2, 1, 12); });
            Assert.Throws(typeof(ArgumentOutOfRangeException), () => { var artInput = new ArtInput(35, [EArtInputCommand.DisableInput, EArtInputCommand.DisableInput, EArtInputCommand.None], 2, 0, 12); });
        }
        [Test]
        public void ArtRDM()
        {
            ArtRDM src = new ArtRDM(1, new Address(3, 4), new RDMMessage() { SourceUID = new RDMUID(0x1122, 0x33445566), DestUID = new RDMUID(0x3344, 0x55667788) });
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtRDM des)
                Assert.That(des, Is.EqualTo(src));

            src = new ArtRDM(new PortAddress(1, 2, 5), new RDMMessage() { SourceUID = new RDMUID(0x1122, 0x33445566), DestUID = new RDMUID(0x3344, 0x55667788) });
            array = src;
            Assert.That(Tools.TryDeserializePacket(array, out res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtRDM des2)
                Assert.That(des2, Is.EqualTo(src));
        }
        [Test]
        public void ArtRDMSub()
        {
            ArtRDMSub src = new ArtRDMSub(new RDMUID(0x1122,0x33445566),234,52354,45647,43432, new byte[] { 0xf0, 0xaa, 0xbb, 0x13, 0x13, 0x19, 0x1f });
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtRDMSub des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtTodControl()
        {
            ArtTodControl src = new ArtTodControl(1, new Address(3, 4));
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtTodControl des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtTodRequest()
        {
            ArtTodRequest src = new ArtTodRequest(1, new Address[] {
                new Address(3, 4),
                new Address(0, 0),
                new Address(1, 0),
                new Address(0, 1),
                new Address(0x0, 0xf),
                new Address(0xf, 0x0),
                new Address(0xf, 0xf) });
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtTodRequest des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtTodData()
        {
            ArtTodData src = new ArtTodData(1,
                                            new Address(3, 4),
                                            3,
                                            2,
                                            204,
                                            11,
                                            new RDMUID[] { new RDMUID(0x0220, 0x11223344), new RDMUID(0x1111, 0x12345678), new RDMUID(0x7e17, 0xefa8e1ee) },
                                            EArtTodDataCommandResponse.TodNak,
                                            ERDMVersion.DRAFT_V1_0);
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtTodData des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtIpProg()
        {
            ArtIpProg src = new ArtIpProg(new byte[] {1,2,3,4}, new byte[] { 5, 6, 7, 8 }, new byte[] { 9, 10, 11, 12 }, EArtIpProgCommand.EnableProgramming);
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtIpProg des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtIpProgReply()
        {
            ArtIpProgReply src = new ArtIpProgReply(new byte[] { 1, 2, 3, 4 }, new byte[] { 5, 6, 7, 8 }, new byte[] { 9, 10, 11, 12 }, EArtIpProgReplyStatusFlags.EnableDHCP);
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtIpProgReply des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtTimeCode()
        {
            ArtTimeCode src = new ArtTimeCode(1, 2, 3, 4, ETimecodeType.SMTPE);
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtTimeCode des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtTimeSync()
        {
            ArtTimeSync src = new ArtTimeSync(true, DateTime.Now, EDaylightSaving.Active);
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtTimeSync des)
                Assert.That(des, Is.EqualTo(src));

            src = new ArtTimeSync(true, DateTime.Now, EDaylightSaving.Inactive);
            Assert.That(res.GetHashCode(), Is.Not.EqualTo(src.GetHashCode()));
        }
        [Test]
        public void ArtAddress()
        {
            ArtAddress src = new ArtAddress(4, 6, 7, new Universe?[] { 1 }, new Universe?[] { 8 }, "sdadf", "dadad", 23, new ArtAddressCommand(EArtAddressCommand.MergeLtp, 2));
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtAddress des)
                Assert.That(des, Is.EqualTo(src));

            src = new ArtAddress(4, 6, 7, new Universe?[] { 1 }, new Universe?[] { 8 }, "sdadf", "dadfd", 23, new ArtAddressCommand(EArtAddressCommand.MergeLtp, 2));
            Assert.That(res.GetHashCode(), Is.Not.EqualTo(src.GetHashCode()));
        }
        [Test]
        public void ArtData()
        {
            ArtData src = new ArtData(1234,5678, EDataRequest.UrlProduct);
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtData des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtDataReply()
        {
            ArtDataReply src = new ArtDataReply(1234, 5678, EDataRequest.UrlProduct, "TestUrl");
            byte[] array = src;
            Assert.That(Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            Assert.That(res.GetHashCode(), Is.EqualTo(src.GetHashCode()));
            doImplicidTests(src);
            if (res is ArtDataReply des)
                Assert.That(des, Is.EqualTo(src));
        }

        private void doImplicidTests(AbstractArtPacketCore packet)
        {
            List<byte[]> dataList = new List<byte[]>();
            dataList.Add(packet);
            if (packet is AbstractArtPacket abstractArtPacket)
                dataList.Add(abstractArtPacket);
            if (packet is AbstractArtPacketNet abstractArtPacketNet)
                dataList.Add(abstractArtPacketNet);
            if (packet is AbstractArtPacketNetAddress abstractArtPacketNetAddress)
                dataList.Add(abstractArtPacketNetAddress);

            foreach (var data in dataList)
            {
                Assert.That(Tools.TryDeserializePacket(data, out var res), Is.True);
                Assert.That(res.GetType(), Is.EqualTo(packet.GetType()));
                Assert.That(res, Is.EqualTo(packet));
                Assert.That(res == packet, Is.True);
                Assert.That(res != packet, Is.False);
                Assert.That(null == packet, Is.False);
                Assert.That(null == res, Is.False);
                Assert.That(packet == null, Is.False);
                Assert.That(res == null, Is.False);
                Assert.That(null != packet, Is.True);
                Assert.That(null != res, Is.True);
                Assert.That(packet != null, Is.True);
                Assert.That(res != null, Is.True);
                Assert.That(packet!.Equals(res), Is.True);
                Assert.That(packet.Equals((object)res), Is.True);
                Assert.That(res.Equals(packet), Is.True);
                Assert.That(res.Equals((object)packet), Is.True);
                Assert.That(packet.Equals(null), Is.False);
                Assert.That(res.Equals(null), Is.False);
                Assert.That(res.GetHashCode(), Is.EqualTo(packet.GetHashCode()));
                Assert.That(res is IDisposableExtended, Is.True);
                if (res is IDisposableExtended ide)
                {
                    Assert.That(ide.IsDisposed, Is.False);
                    Assert.That(ide.IsDisposing, Is.False);
                    ide.Dispose();
                    Assert.That(ide.IsDisposed, Is.True);
                    ide.Dispose();
                    Assert.That(ide.IsDisposed, Is.True);

                    Assert.Throws(typeof(ObjectDisposedException), () => res.GetPacket());
                }
                Assert.That(res.ToString(), Is.Not.Empty);
                res = null!;
                Assert.That(null == res, Is.True);
                Assert.That(res == null, Is.True);
                Assert.That(null != res, Is.False);
                Assert.That(res != null, Is.False);
            }
            Assert.That(packet.ToString(), Is.Not.Empty);
            packet = null!;
            Assert.That(null == packet, Is.True);
            Assert.That(packet == null, Is.True);
            Assert.That(null != packet, Is.False);
            Assert.That(packet != null, Is.False);
        }
    }
}