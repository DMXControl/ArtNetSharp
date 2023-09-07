using ArtNetSharp;
using Tools = ArtNetSharp.Tools;
using RDMSharp;
using RDMTools = RDMSharp.Tools;

namespace ArtNetTests
{
    public class PackagesSerializeDeserialize
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void ArtSync()
        {
            ArtSync src = new ArtSync();
            byte[] array = src;
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
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
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtPoll des)
                Assert.That(des, Is.EqualTo(src));
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
                                                ENodeStatus.DHCP_ConfigurationSupported
                                                    | ENodeStatus.DHCP_ConfigurationUsed
                                                    | ENodeStatus.IndicatorStateLocate
                                                    | ENodeStatus.NodeSupports15BitPortAddress
                                                    | ENodeStatus.RDM_Supported,
                                                3,
                                                6,
                                                new Net(8),
                                                new Subnet(14),
                                                new Universe[] { new Universe(12) },
                                                new Universe[] { new Universe(9) },
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
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtPollReply des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtDMX()
        {
            ArtDMX src = new ArtDMX(34, 0, 2, new Address(3, 4), new byte[] { 1, 2, 3, 44, 55, 66, 88, 222, 111, 0x33 });
            byte[] array = src;
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtDMX des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtNzs()
        {
            ArtNzs src = new ArtNzs(53, 0xED, 2, new Address(3, 4), new byte[] { 1, 2, 3, 44, 55, 66, 88, 222, 111, 0x33 });
            byte[] array = src;
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
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
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtVlc des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtInput()
        {
            ArtInput src = new ArtInput(33, new EArtInputCommand[] { EArtInputCommand.DisableInput, EArtInputCommand.DisableInput, EArtInputCommand.None }, 3, 0, 23);
            byte[] array = src;
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtInput des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtRDM()
        {
            ArtRDM src = new ArtRDM(1, new Address(3, 4), new RDMMessage() { SourceUID = new RDMUID(0x1122, 0x33445566), DestUID = new RDMUID(0x3344, 0x55667788) });
            byte[] array = src;
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtRDM des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtRDMSub()
        {
            ArtRDMSub src = new ArtRDMSub(new RDMUID(0x1122,0x33445566),234,52354,45647,43432, new byte[] { 0xf0, 0xaa, 0xbb, 0x13, 0x13, 0x19, 0x1f });
            byte[] array = src;
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtRDMSub des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtTodControl()
        {
            ArtTodControl src = new ArtTodControl(1, new Address(3, 4));
            byte[] array = src;
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
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
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
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
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtTodData des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtIpProg()
        {
            ArtIpProg src = new ArtIpProg(new byte[] {1,2,3,4}, new byte[] { 5, 6, 7, 8 }, new byte[] { 9, 10, 11, 12 }, EArtIpProgCommand.EnableProgramming);
            byte[] array = src;
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtIpProg des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtIpProgReply()
        {
            ArtIpProgReply src = new ArtIpProgReply(new byte[] { 1, 2, 3, 4 }, new byte[] { 5, 6, 7, 8 }, new byte[] { 9, 10, 11, 12 }, EArtIpProgReplyStatusFlags.EnableDHCP);
            byte[] array = src;
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtIpProgReply des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtTimeCode()
        {
            ArtTimeCode src = new ArtTimeCode(1, 2, 3, 4, ETimecodeType.SMTPE);
            byte[] array = src;
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtTimeCode des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtTimeSync()
        {
            ArtTimeSync src = new ArtTimeSync(true, DateTime.Now, EDaylightSaving.Active);
            byte[] array = src;
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtTimeSync des)
                Assert.That(des, Is.EqualTo(src));
        }
        [Test]
        public void ArtAddress()
        {
            ArtAddress src = new ArtAddress(4, 6, 7, new Universe?[] { 1 }, new Universe?[] { 8 }, "sdadf", "dadad", 23, new ArtAddressCommand(EArtAddressCommand.MergeLtp, 2));
            byte[] array = src;
            var res = Tools.DeserializePacket(array);
            Assert.That(res.GetType(), Is.EqualTo(src.GetType()));
            if (res is ArtAddress des)
                Assert.That(des, Is.EqualTo(src));
        }
    }
}