using ArtNetSharp;
using ArtNetSharp.Messages.Interfaces;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;

namespace ArtNetTests
{
    [Order(2)]
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
            Assert.That(ArtNetSharp.Tools.TryDeserializePacket(data, out _), Is.False);

            data = [];
            Assert.That(ArtNetSharp.Tools.TryDeserializePacket(data, out _), Is.False);
            data = [0x00, 0x01];
            Assert.That(ArtNetSharp.Tools.TryDeserializePacket(data, out _), Is.False);

            data = new byte[12];
            Assert.That(ArtNetSharp.Tools.TryDeserializePacket(data, out _), Is.False);

            ArtTodControl src = new ArtTodControl(1, new Address(3, 4));
            data = src;
            data = data.Take(22).ToArray();
            Assert.That(ArtNetSharp.Tools.TryDeserializePacket(data, out _), Is.False);
        }
        [Test]
        public void Tools_TryDeserializePacketFromRaw()
        {
            byte[]? data = new byte[] { 0x41, 0x72, 0x74, 0x2d, 0x4e, 0x65, 0x74, 0x00, 0x00, 0x50, 0x00, 0x0e, 0x1a, 0x01, 0x01, 0x00, 0x02, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x4d, 0x14, 0xff, 0xff, 0xff, 0x33, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x4d, 0x14, 0xff, 0xff, 0xff, 0x33, 0x80, 0x00, 0x00, 0x59, 0x14, 0xff, 0xff, 0xff, 0x33, 0x80, 0x00, 0x00, 0x1c, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x1c, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x59, 0x14, 0xff, 0xff, 0xff };
            Assert.That(ArtNetSharp.Tools.TryDeserializePacket(data, out AbstractArtPacketCore packet), Is.True);
            
            data = new byte[] { 0x41, 0x72, 0x74, 0x2d, 0x4e, 0x65, 0x74, 0x00, 0x00, 0x50, 0x00, 0x0e, 0x1a, 0x01, 0x01, 0x00, 0x02, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x50, 0x6f, 0x72, 0x74, 0x20, 0x32, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x4d, 0x14, 0xff, 0xff, 0xff, 0x33, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x02, 0x00, 0x01, 0x40, 0xff, 0xaa, 0x80, 0x80, 0x00, 0x02, 0x14, 0x00, 0xff, 0xff, 0xaa, 0x01, 0x00, 0x00, 0x02, 0x01, 0xff, 0xff, 0xff, 0x64, 0x80, 0x00, 0x00, 0x4d, 0x14, 0xff, 0xff, 0xff, 0x33, 0x80, 0x00, 0x00, 0x59, 0x14, 0xff, 0xff, 0x02, 0x33, 0xc0, 0x00, 0x00, 0x1c, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x1c, 0x14, 0xff, 0xff, 0xff, 0xaa, 0x80, 0x00, 0x00, 0x59, 0x14, 0xff, 0xff, 0xff };
            Assert.That(ArtNetSharp.Tools.TryDeserializePacket(data, out packet), Is.True);
        }

        [Test]
        public void ArtSync()
        {
            PackagesSerializeDeserialize.doTests(new ArtSync());
        }
        [Test]
        public void ArtPoll()
        {
            PackagesSerializeDeserialize.doTests(new ArtPoll(
                0x1001,
                0xea0f,
                0x0001,
                0x0f00,
                EArtPollFlags.DiagnosticEnabled | EArtPollFlags.DiagnosticUnicast,
                EPriorityCode.DpMed));

            PackagesSerializeDeserialize.doTests(new ArtPoll(
                oemCode: 0x1001,
                manufacturerCode: 0xea0f,
                flags:
                EArtPollFlags.DiagnosticEnabled | EArtPollFlags.DiagnosticUnicast,
                priority: EPriorityCode.DpMed));
        }
        [Test]
        public void ArtPollReply()
        {
            PackagesSerializeDeserialize.doTests(new ArtPollReply(
                new IPv4Address("2.3.2.3"),
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
                    rDM_Supported: true,
                    backgroundQueueSupported:true),


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
                new GoodInput[] { new GoodInput() },
                new GoodOutput[] { new GoodOutput(outputStyle: GoodOutput.EOutputStyle.Continuous, rdmIsDisabled: true, isBeingOutputAsDMX: true, dmx_OutputShortCircuit: true) },
                EMacroState.Macro1Active
                    | EMacroState.Macro2Active,
                ERemoteState.Remote3Active
                    | ERemoteState.Remote4Active,
                44,
                77,
                4,
                44,
                EBackgroundQueuePolicy.STATUS_NONE,
                EStCodes.StConfig,
                new UID(0x1122, 0x33445566)));
            PackagesSerializeDeserialize.doTests(new ArtPollReply(
                new IPv4Address("2.3.2.3"),
                new IPv4Address("4.4.4.4"),
                new MACAddress(2, 44, 5, 77, 8, 34),
                null,
                null,
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
                new GoodInput[] { new GoodInput(GoodInput.EConvertTo.sACN, true, true, false, true) },
                new GoodOutput[] { new GoodOutput(outputStyle: GoodOutput.EOutputStyle.Continuous, rdmIsDisabled: true, isBeingOutputAsDMX: true, dmx_OutputShortCircuit: true) },
                EMacroState.Macro1Active
                    | EMacroState.Macro2Active,
                ERemoteState.Remote3Active
                    | ERemoteState.Remote4Active,
                44,
                77,
                4,
                44,
                EBackgroundQueuePolicy.STATUS_ADVISORY,
                EStCodes.StConfig,
                new UID(0x1122, 0x33445566)));

            PackagesSerializeDeserialize.doTests(new ArtPollReply(
                new IPv4Address("2.3.2.3"),
                new IPv4Address("4.4.4.4"),
                new MACAddress(2, 44, 5, 77, 8, 34),
                "sdjsdsdjosdkposkdlijvlsad,f�la,df�lalkadmv�lsa,v�las,vlasdfksdmf�lsa,vlsdfkds�l,asldfk�lsadf,�lafd�lamf",
                "hsdsdjfkksfkadf�lafdakfaf�laoggaflsakf�lakoejgg�ladmfka�lf�lagoijtefp;D�LGTRJGPWREKGPOWEGKJPVKAODKFJOIEWRJFOWEFOWEFJ",
                3,
                new NodeStatus(
                    dHCP_ConfigurationSupported: true,
                    dHCP_ConfigurationUsed: true,
                    indicatorState: NodeStatus.EIndicatorState.Locate,
                    portAddressBitResolution: NodeStatus.EPortAddressBitResolution._15Bit,
                    rDM_Supported: true),
                3,
                6,
                new Address(2, 12),
                new Address(2, 9)), true);

            doPortRelatedExceptionTests(5);
            doPortRelatedExceptionTests(1, oemCode: 0);
            doPortRelatedExceptionTests(1, manufacturerCode: 0);
            doPortRelatedExceptionTests(5);
            doPortRelatedExceptionTests(1, _inputs: new object[] { new Universe(15), new Universe(3), new Universe(9), new Universe(5), new Universe(1) });
            doPortRelatedExceptionTests(1, _outputs: new object[] { new Universe(15), new Universe(3), new Universe(9), new Universe(5), new Universe(1) });
            doPortRelatedExceptionTests(2, portTypes: new EPortType[] { EPortType.ArtNet | EPortType.InputToArtNet, EPortType.MIDI, EPortType.MIDI, EPortType.MIDI, EPortType.MIDI });
            doPortRelatedExceptionTests(2, goodInputs: new GoodInput[] { new GoodInput(), new GoodInput(receiveErrorsDetected: true), new GoodInput(dMX_TextPacketsSupported: true), new GoodInput(dMX_TextPacketsSupported: true), new GoodInput(dMX_TextPacketsSupported: true), new GoodInput(dMX_TextPacketsSupported: true) });
            doPortRelatedExceptionTests(2, goodOutputs: new GoodOutput[] { new GoodOutput(), new GoodOutput(dmx_OutputShortCircuit: true), new GoodOutput(dmx_OutputShortCircuit: true), new GoodOutput(dmx_OutputShortCircuit: true), new GoodOutput(dmx_OutputShortCircuit: true) });

            void doPortRelatedExceptionTests(
                byte ports,
                object[]? _inputs = null,
                object[]? _outputs = null,
                EPortType[]? portTypes = null,
                GoodInput[]? goodInputs = null,
                GoodOutput[]? goodOutputs = null,
                ushort oemCode = 1234,
                ushort manufacturerCode = 1234)
            {

                Assert.Throws(typeof(ArgumentOutOfRangeException), () =>
                {
                    new ArtPollReply(
                        new IPv4Address("2.3.2.3"),
                        new IPv4Address("4.4.4.4"),
                        new MACAddress(2, 44, 5, 77, 8, 34),
                        "sdjsdsdjosdkposkdlijvlsad,f�la,df�lalkadmv�lsa,v�las,vlasdfksdmf�lsa,vlsdfkds�l,asldfk�lsadf,�lafd�lamf",
                        "hsdsdjfkksfkadf�lafdakfaf�laoggaflsakf�lakoejgg�ladmfka�lf�lagoijtefp;D�LGTRJGPWREKGPOWEGKJPVKAODKFJOIEWRJFOWEFOWEFJ",
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
                        _inputs,
                        _outputs,
                        oemCode,
                        manufacturerCode,
                        ports,
                        new NodeReport(ENodeReportCodes.RcFactoryRes, "ewqewfffafdafafgg", 55),
                        portTypes,
                        goodInputs,
                        goodOutputs,
                        EMacroState.Macro1Active
                            | EMacroState.Macro2Active,
                        ERemoteState.Remote3Active
                            | ERemoteState.Remote4Active,
                        44,
                        77,
                        4,
                        44,
                        EBackgroundQueuePolicy.STATUS_NONE,
                        EStCodes.StConfig,
                        new UID(0x1122, 0x33445566));
                });
            }
        }
        [Test]
        public void ArtDMX()
        {
            PackagesSerializeDeserialize.doTests(new ArtDMX(34, 0, 2, new Address(3, 4), new byte[] { 1, 2, 3, 44, 55, 66, 88, 222, 111, 0x33 }));
            PackagesSerializeDeserialize.doTests(new ArtDMX(34, 0, 2, new Address(3, 4), new byte[513]));
        }
        [Test]
        public void ArtNzs()
        {
            PackagesSerializeDeserialize.doTests(new ArtNzs(53, 0xED, 2, new Address(3, 4), new byte[] { 1, 2, 3, 44, 55, 66, 88, 222, 111, 0x33 }));

            Assert.Throws(typeof(ArgumentException), () => new ArtNzs(53, ArtNetSharp.Constants.DMX_STARTCODE, 2, new Address(3, 4), new byte[] { 1, 2, 3, 44, 55, 66, 88, 222, 111, 0x33 }));
            Assert.Throws(typeof(ArgumentException), () => new ArtNzs(53, ArtNetSharp.Constants.RDM_STARTCODE, 2, new Address(3, 4), new byte[] { 1, 2, 3, 44, 55, 66, 88, 222, 111, 0x33 }));
        }
        [Test]
        public void ArtVlc()
        {
            PackagesSerializeDeserialize.doTests(new ArtVlc(
                59,
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
                EPayloadLanguageCode.BeaconText));

            Assert.Throws(typeof(ArgumentOutOfRangeException), () => new ArtVlc(
                59,
                9,
                new Address(5, 1),
                new byte[481],
                0xeacb,
                0xabcd,
                3,
                5,
                435,
                1122,
                666,
                EVlcFlags.Beacon | EVlcFlags.IEEE | EVlcFlags.Reply,
                EPayloadLanguageCode.BeaconText));

            byte[] data = new ArtVlc(
                59,
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

            data[20] = 0;
            Assert.Throws(typeof(ArgumentException), () => new ArtVlc(data));
            data[19] = 0;
            Assert.Throws(typeof(ArgumentException), () => new ArtVlc(data));
            data[18] = 0;
            Assert.Throws(typeof(ArgumentException), () => new ArtVlc(data));
            data[13] = 0;
            Assert.Throws(typeof(ArgumentException), () => new ArtVlc(data));

        }
        [Test]
        public void ArtInput()
        {
            PackagesSerializeDeserialize.doTests(new ArtInput(33, [EArtInputCommand.DisableInput, EArtInputCommand.DisableInput, EArtInputCommand.None], 3, 0, 23));

            Assert.Throws(typeof(ArgumentOutOfRangeException), () => { var artInput = new ArtInput(33, [], 5, 0, 23); });
            Assert.Throws(typeof(ArgumentOutOfRangeException), () => { var artInput = new ArtInput(35, [], 2, 1, 12); });
            Assert.Throws(typeof(ArgumentOutOfRangeException), () => { var artInput = new ArtInput(35, [EArtInputCommand.DisableInput, EArtInputCommand.DisableInput, EArtInputCommand.None], 2, 0, 12); });
        }
        [Test]
        public void ArtRDM()
        {
            PackagesSerializeDeserialize.doTests(new ArtRDM(1, new Address(3, 4), new RDMMessage() { SourceUID = new UID(0x1122, 0x33445566), DestUID = new UID(0x3344, 0x55667788) }));
            PackagesSerializeDeserialize.doTests(new ArtRDM(new PortAddress(1, 2, 5), new RDMMessage() { SourceUID = new UID(0x1122, 0x33445566), DestUID = new UID(0x3344, 0x55667788) }));
            PackagesSerializeDeserialize.doTests(new ArtRDM(new PortAddress(1, 2, 5), new RDMMessage() { SourceUID = new UID(0x1122, 0x33445566), DestUID = new UID(0x3344, 0x55667788) }, fifoAvailable: 30, fifoMax: 200));
            Assert.Throws(typeof(ArgumentNullException), () => new ArtRDM(new PortAddress(1, 2, 5), null));
        }
        [Test]
        public void ArtRDMSub()
        {
            PackagesSerializeDeserialize.doTests(new ArtRDMSub(new UID(0x1122, 0x33445566), 234, 52354, 45647, 43432, new byte[] { 0xf0, 0xaa, 0xbb, 0x13, 0x13, 0x19, 0x1f }));
        }
        [Test]
        public void ArtTodControl()
        {
            PackagesSerializeDeserialize.doTests(new ArtTodControl(1, new Address(3, 4)));
            PackagesSerializeDeserialize.doTests(new ArtTodControl(new PortAddress(5, 3, 4)));
        }
        [Test]
        public void ArtTodRequest()
        {
            PackagesSerializeDeserialize.doTests(new ArtTodRequest(new PortAddress(1, 2, 3)));
            PackagesSerializeDeserialize.doTests(new ArtTodRequest(4, new Address(2, 3)));
            PackagesSerializeDeserialize.doTests(new ArtTodRequest(1, [
                new Address(3, 4),
                new Address(0, 0),
                new Address(1, 0),
                new Address(0, 1),
                new Address(0x0, 0xf),
                new Address(0xf, 0x0),
                new Address(0xf, 0xf) ]));
        }
        [Test]
        public void ArtTodData()
        {
            PackagesSerializeDeserialize.doTests(new ArtTodData(
                1,
                new Address(3, 4),
                3,
                2,
                204,
                11,
                [new UID(0x0220, 0x11223344), new UID(0x1111, 0x12345678), new UID(0x7e17, 0xefa8e1ee)],
                EArtTodDataCommandResponse.TodNak,
                ERDMVersion.DRAFT_V1_0));
            PackagesSerializeDeserialize.doTests(new ArtTodData(
                new PortAddress(9, 3, 4),
                3,
                2,
                204,
                11,
                [new UID(0x0220, 0x11223344), new UID(0x1111, 0x12345678), new UID(0x7e17, 0xefa8e1ee)],
                EArtTodDataCommandResponse.TodNak,
                ERDMVersion.DRAFT_V1_0));

            Random rnd = new Random();
            List<UID> uids = new List<UID>();
            for (int i = 0; i < 210; i++)
                uids.Add(new UID((ulong)rnd.NextInt64()));
            Assert.Throws(typeof(ArgumentOutOfRangeException), () => new ArtTodData(
                new PortAddress(9, 5, 7),
                3,
                2,
                204,
                11,
                uids.ToArray(),
                EArtTodDataCommandResponse.TodNak,
                ERDMVersion.DRAFT_V1_0));
        }
        [Test]
        public void ArtIpProg()
        {
            PackagesSerializeDeserialize.doTests(new ArtIpProg(new byte[] { 1, 2, 3, 4 }, new byte[] { 5, 6, 7, 8 }, new byte[] { 9, 10, 11, 12 }, EArtIpProgCommand.EnableProgramming));
        }
        [Test]
        public void ArtIpProgReply()
        {
            PackagesSerializeDeserialize.doTests(new ArtIpProgReply(new byte[] { 1, 2, 3, 4 }, new byte[] { 5, 6, 7, 8 }, new byte[] { 9, 10, 11, 12 }, EArtIpProgReplyStatusFlags.EnableDHCP));
        }
        [Test]
        public void ArtTimeCode()
        {
            PackagesSerializeDeserialize.doTests(new ArtTimeCode(5, 1, 2, 3, 4, ETimecodeType.SMTPE));

            Assert.Throws(typeof(ArgumentOutOfRangeException), () => new ArtTimeCode(6, 30, 2, 3, 4, ETimecodeType.SMTPE));
            Assert.Throws(typeof(ArgumentOutOfRangeException), () => new ArtTimeCode(7, 1, 60, 3, 4, ETimecodeType.SMTPE));
            Assert.Throws(typeof(ArgumentOutOfRangeException), () => new ArtTimeCode(8, 1, 2, 60, 4, ETimecodeType.SMTPE));
            Assert.Throws(typeof(ArgumentOutOfRangeException), () => new ArtTimeCode(0, 1, 2, 3, 24, ETimecodeType.SMTPE));
        }
        [Test]
        public void ArtTimeSync()
        {
            ArtTimeSync src = new ArtTimeSync(true, DateTime.UtcNow, EDaylightSaving.Active);
            PackagesSerializeDeserialize.doTests(src);

            ArtTimeSync src2 = new ArtTimeSync(true, DateTime.UtcNow, EDaylightSaving.Inactive);
            Assert.That(src.GetHashCode(), Is.Not.EqualTo(src2.GetHashCode()));

            PackagesSerializeDeserialize.doTests(new ArtTimeSync(false, DateTime.UtcNow, EDaylightSaving.Active));
        }
        [Test]
        public void ArtAddress_Test()
        {
            ArtAddress src = new ArtAddress(4, 6, 7, new Universe?[] { 1 }, new Universe?[] { 8 }, "sdadf", "dadad", 23, new ArtAddressCommand(EArtAddressCommand.MergeLtp, 2));
            PackagesSerializeDeserialize.doTests(src);

            ArtAddress src2 = new ArtAddress(4, 6, 7, new Universe?[] { 1 }, new Universe?[] { 8 }, "sdadf", "dadfd", 23, new ArtAddressCommand(EArtAddressCommand.MergeLtp, 2));
            Assert.That(src.GetHashCode(), Is.Not.EqualTo(src2.GetHashCode()));

            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetName(1, "Test Short", "Test Long", new ArtAddressCommand(EArtAddressCommand.FailFull)));
            Assert.DoesNotThrow(() => PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetAcnPriority(2, 255, new ArtAddressCommand(EArtAddressCommand.FailHold))));
            Assert.Throws(typeof(ArgumentOutOfRangeException), () => ArtAddress.CreateSetAcnPriority(3, 250, new ArtAddressCommand(EArtAddressCommand.FailRecord)));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetCommand(4, new ArtAddressCommand(EArtAddressCommand.DirectionRx, 3)));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetNet(5, 3));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetSubnet(6, 3));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetNetSubnet(7, 3, 4));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetInputUniverse(8, 4, 3, 4));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetInputUniverse(9, new PortAddress(1, 2, 3)));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetInputUniverse(10, new Universe(3)));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetInputUniverse(11, [new Universe(1)]));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetInputUniverse(12, 3, 4, [new Universe(1)]));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetOutputUniverse(13, 4, 3, 4));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetOutputUniverse(14, new PortAddress(1, 2, 3)));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetOutputUniverse(15, new Universe(3)));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetOutputUniverse(16, [new Universe(1)]));
            PackagesSerializeDeserialize.doTests(ArtAddress.CreateSetOutputUniverse(17, 3, 4, [new Universe(1)]));
        }
        [Test]
        public void ArtData()
        {
            PackagesSerializeDeserialize.doTests(new ArtData(1234, 5678, EDataRequest.UrlProduct));
        }
        [Test]
        public void ArtDataReply()
        {
            PackagesSerializeDeserialize.doTests(new ArtDataReply(1234, 5678, EDataRequest.UrlProduct, "TestUrl"));
            PackagesSerializeDeserialize.doTests(new ArtDataReply(1234, 5678, EDataRequest.UrlProduct, payload: null));
        }

        private static void doTests<T>(T packet, bool toLongStrings = false) where T : AbstractArtPacketCore
        {
            byte[] array = packet;
            Assert.That(ArtNetSharp.Tools.TryDeserializePacket(array, out var res), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(res.GetType(), Is.EqualTo(packet.GetType()));
                Assert.That(res.GetHashCode(), Is.EqualTo(packet.GetHashCode()));
            });
            if (!toLongStrings)
                PackagesSerializeDeserialize.doImplicidTests(packet);
            else return;
            if (res is T des)
                Assert.That(des, Is.EqualTo(packet));
        }
        private static void doImplicidTests(AbstractArtPacketCore packet)
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
                Assert.That(ArtNetSharp.Tools.TryDeserializePacket(data, out var res), Is.True);
                Assert.Multiple(() =>
                {
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
                    Assert.That(packet.Equals((object)res!), Is.True);
                    Assert.That(res!.Equals(packet), Is.True);
                    Assert.That(res.Equals((object)packet), Is.True);
                    Assert.That(packet.Equals(null), Is.False);
                    Assert.That(res.Equals(null), Is.False);
                    Assert.That(res!.GetHashCode(), Is.EqualTo(packet!.GetHashCode()));
                    Assert.That(res is IDisposableExtended, Is.True);
                });
                if (res is IDisposableExtended ide)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(ide.IsDisposed, Is.False);
                        Assert.That(ide.IsDisposing, Is.False);
                    });
                    ide.Dispose();
                    Assert.That(ide.IsDisposed, Is.True);
                    ide.Dispose();
                    Assert.That(ide.IsDisposed, Is.True);

                    Assert.Throws(typeof(ObjectDisposedException), () => res.GetPacket());
                }
                Assert.Multiple(() =>
                {
                    Assert.That(res.ToString(), Is.Not.Empty);
                    res = null!;
                    Assert.That(null == res, Is.True);
                    Assert.That(res == null, Is.True);
                    Assert.That(null != res, Is.False);
                    Assert.That(res != null, Is.False);
                });
            }
            Assert.Multiple(() =>
            {
                Assert.That(packet.ToString(), Is.Not.Empty);
                packet = null!;
                Assert.That(null == packet, Is.True);
                Assert.That(packet == null, Is.True);
                Assert.That(null != packet, Is.False);
                Assert.That(packet != null, Is.False);
            });
        }


        [Test]
        public async Task AbstractArtPacketCore()
        {
            var artPoll = new ArtPoll().GetPacket();
            Assert.Throws(typeof(ArgumentException), () => { new MockPacketCore(artPoll); });
            var artSync = new ArtSync().GetPacket();
            Assert.Throws(typeof(ArgumentOutOfRangeException), () => { new MockPacketCore(artSync); });
            byte[] toDisposeMock = new byte[18];
            Array.Copy(artSync, toDisposeMock, artSync.Length);
            var mock = new MockPacketCore(toDisposeMock);
            Assert.That(((IDisposableExtended)mock).IsDisposed, Is.False);
            _ = Task.Run(async () =>
            {
                await Task.CompletedTask;
                ((IDisposable)mock).Dispose();
            });
            await Task.Delay(100);
            Assert.That(((IDisposableExtended)mock).IsDisposed, Is.False);
            Assert.That(((IDisposableExtended)mock).IsDisposing, Is.True);
            Assert.Throws(typeof(ObjectDisposedException), () => { mock.GetPacket(); });
            Assert.DoesNotThrow(() => { ((IDisposable)mock).Dispose(); });
            Assert.That(((IDisposableExtended)mock).IsDisposed, Is.False);
            Assert.That(((IDisposableExtended)mock).IsDisposing, Is.True);
            mock.EndDispose();
            await Task.Delay(200);
            Assert.That(((IDisposableExtended)mock).IsDisposed, Is.True);
            Assert.That(((IDisposableExtended)mock).IsDisposing, Is.False);
        }
        class MockPacketCore : AbstractArtPacketCore
        {

            public MockPacketCore(in byte[] data) : base(data)
            {
            }

            public override EOpCodes OpCode => EOpCodes.OpSync;

            protected override ushort PacketMinLength => 18;
            protected override ushort PacketMaxLength => 19;

            private bool holdDispose;
            public void EndDispose()
            {
                holdDispose = false;
            }
            protected override void Dispose()
            {
                holdDispose = true;
                while (holdDispose)
                    Thread.Sleep(10);

                throw new Exception("Mock Exception");
            }


            protected override void fillPacketCore(ref byte[] packet)
            {
                throw new NotImplementedException();
            }
        }

    }
}