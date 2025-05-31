using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetSharp.Misc;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;

namespace ArtNetTests
{
    [Order(1)]
    public class ObjectTypesTests
    {
        [Test]
        public void TestNodeReport()
        {
            HashSet<NodeReport> nodeReports = new HashSet<NodeReport>();
            NodeReport src = new NodeReport(ENodeReportCodes.RcFirmwareFail, "FAILED", 337);
            NodeReport dest = new NodeReport(src.ToString());
            Assert.Multiple(() =>
            {
                Assert.That(dest, Is.EqualTo(src));
                Assert.That(dest.Equals(src), Is.True);
                Assert.That(dest.Equals((object)src), Is.True);
                Assert.That(dest.Equals(null), Is.False);
            });

            src = new NodeReport(ENodeReportCodes.RcDebug, "Test", 0);
            for (byte i = 0; i < byte.MaxValue; i++)
            {
                src = src.Increment();
                Assert.That(dest, Is.Not.EqualTo(src));
                Assert.That(dest.Equals(src), Is.False);
                nodeReports.Add(src);
            }
            Assert.That(nodeReports.OrderBy(n => n).ToList(), Has.Count.EqualTo(byte.MaxValue));

            Assert.DoesNotThrow(() => dest = new NodeReport(null));
        }
        [Test]
        public void TestArtAddressCommand()
        {
            EArtAddressCommand[] commandsWithX = {
                EArtAddressCommand.DirectionRx,
                EArtAddressCommand.DirectionTx,
                EArtAddressCommand.MergeHtp,
                EArtAddressCommand.MergeLtp,
                EArtAddressCommand.AcnSel,
                EArtAddressCommand.ArtNetSel,
                EArtAddressCommand.ClearOp,
                EArtAddressCommand.RdmDisable,
                EArtAddressCommand.RdmEnable,
                EArtAddressCommand.StyleConst,
                EArtAddressCommand.StyleDelta,
                EArtAddressCommand.SetBackgroundQueuePolicy};

            EArtAddressCommand[] commandsWithoutX = {
                EArtAddressCommand.LedLocate,
                EArtAddressCommand.LedMute,
                EArtAddressCommand.LedNormal,
                EArtAddressCommand.ResetRxFlags,
                EArtAddressCommand.None,
                EArtAddressCommand.FailZero,
                EArtAddressCommand.FailScene,
                EArtAddressCommand.FailRecord,
                EArtAddressCommand.FailHold,
                EArtAddressCommand.FailFull,
                EArtAddressCommand.AnalysisOff,
                EArtAddressCommand.AnalysisOn,
                EArtAddressCommand.CancelMerge};


            HashSet<ArtAddressCommand> artAddressCommands = new HashSet<ArtAddressCommand>();

            void doTests(EArtAddressCommand command, byte? port)
            {
                string state = $"Command: {command} Port: {port}";
                ArtAddressCommand artAddressCommand = new ArtAddressCommand(command, port);
                Assert.Multiple(() =>
                {
                    Assert.That(artAddressCommand.Command, Is.EqualTo(command), state);
                    Assert.That(artAddressCommand.Port, Is.EqualTo(port), state);
                });
                byte serialized = (byte)artAddressCommand;
                ArtAddressCommand artAddressCommandResult = new ArtAddressCommand(serialized);
                Assert.Multiple(() =>
                {
                    Assert.That(artAddressCommandResult.Command, Is.EqualTo(command), state);
                    Assert.That(artAddressCommandResult.Port, Is.EqualTo(port), state);
                    Assert.That(artAddressCommandResult.GetHashCode(), Is.EqualTo(artAddressCommand.GetHashCode()), state);
                    Assert.That(artAddressCommandResult, Is.EqualTo(artAddressCommand), state);
                    Assert.That(artAddressCommandResult == artAddressCommand, Is.True, state);
                    Assert.That(artAddressCommandResult != artAddressCommand, Is.False, state);
                    Assert.That(artAddressCommandResult.Equals((object)artAddressCommand), Is.True, state);
                    Assert.That(artAddressCommandResult.Equals(artAddressCommand), Is.True, state);
                    Assert.That(artAddressCommandResult.Equals(null), Is.False, state);
                });
                artAddressCommands.Add(artAddressCommand);
            }

            foreach (EArtAddressCommand command in commandsWithX)
                for (byte port = 0; port < 4; port++)
                    doTests(command, port);
            foreach (EArtAddressCommand command in commandsWithoutX)
                doTests(command, null);
            Assert.Multiple(() =>
            {
                Assert.That(artAddressCommands, Has.Count.EqualTo((commandsWithX.Length * 4) + commandsWithoutX.Length));

                Assert.Throws(typeof(ArgumentException), () => new ArtAddressCommand(EArtAddressCommand.MergeHtp, null));
                Assert.Throws(typeof(ArgumentException), () => new ArtAddressCommand(EArtAddressCommand.LedMute, 2));
                Assert.Throws(typeof(ArgumentOutOfRangeException), () => new ArtAddressCommand(EArtAddressCommand.MergeHtp, 6));
                Assert.Throws(typeof(ArgumentOutOfRangeException), () => new ArtAddressCommand(EArtAddressCommand.SetBackgroundQueuePolicy, 16));

                Assert.That(new ArtAddressCommand(EArtAddressCommand.MergeHtp, 1), Is.Not.EqualTo(new ArtAddressCommand(EArtAddressCommand.MergeHtp, 2)));
                Assert.That(new ArtAddressCommand(EArtAddressCommand.MergeLtp, 1), Is.Not.EqualTo(new ArtAddressCommand(EArtAddressCommand.MergeHtp, 2)));
                Assert.That(new ArtAddressCommand(EArtAddressCommand.MergeLtp, 1), Is.Not.EqualTo(new ArtAddressCommand(EArtAddressCommand.MergeHtp, 1)));
                Assert.That(ArtAddressCommand.Default, Is.EqualTo(new ArtAddressCommand(EArtAddressCommand.None, null)));
                Assert.That(ArtAddressCommand.Default.ToString(), Is.Not.Empty);
            });
        }
        [Test]
        public void TestNodeStatus()
        {
            HashSet<NodeStatus> subjects = new HashSet<NodeStatus>();
            for (int i = 0; i < 0xffffff; i += 0b01010101)
                subjects.Add(new NodeStatus((byte)(i & 0xff), (byte)((i >> 8) & 0xff), (byte)((i >> 16) & 0xff)));

            subjects.Add(new NodeStatus(true,
                               true,
                               true,
                               NodeStatus.EPortAddressProgrammingAuthority.NotUsed,
                               NodeStatus.EIndicatorState.Normal,
                               true,
                               true,
                               true,
                               NodeStatus.EPortAddressBitResolution._15Bit,
                               true,
                               true,
                               true,
                               true,
                               true,
                               true,
                               true,
                               NodeStatus.EFailsafeState.PlaybackScene));
            subjects.Add(new NodeStatus(true,
                               true,
                               true,
                               NodeStatus.EPortAddressProgrammingAuthority.ByNetwork,
                               NodeStatus.EIndicatorState.Mute,
                               true,
                               true,
                               true,
                               NodeStatus.EPortAddressBitResolution._8Bit,
                               true,
                               true,
                               true,
                               true,
                               true,
                               true,
                               true,
                               NodeStatus.EFailsafeState.AllFull));
            subjects.Add(new NodeStatus(true,
                               true,
                               true,
                               NodeStatus.EPortAddressProgrammingAuthority.ByFrontPanel,
                               NodeStatus.EIndicatorState.Locate,
                               true,
                               true,
                               true,
                               NodeStatus.EPortAddressBitResolution._8Bit,
                               true,
                               true,
                               true,
                               true,
                               true,
                               true,
                               true,
                               NodeStatus.EFailsafeState.AllZero));
            foreach (NodeStatus nodeStatus in subjects)
            {
                NodeStatus result = new NodeStatus(nodeStatus.StatusByte1, nodeStatus.StatusByte2, nodeStatus.StatusByte3);
                Assert.Multiple(() =>
                {
                    Assert.That(nodeStatus.StatusByte1, Is.EqualTo(result!.StatusByte1));
                    Assert.That(nodeStatus.StatusByte2, Is.EqualTo(result!.StatusByte2));
                    Assert.That(nodeStatus.StatusByte3, Is.EqualTo(result!.StatusByte3));
                    Assert.That(nodeStatus.RDM_Supported, Is.EqualTo(result!.RDM_Supported));
                    Assert.That(nodeStatus.ROM_Booted, Is.EqualTo(result!.ROM_Booted));
                    Assert.That(nodeStatus.PortAddressProgrammingAuthority, Is.EqualTo(result!.PortAddressProgrammingAuthority));
                    Assert.That(nodeStatus.IndicatorState, Is.EqualTo(result!.IndicatorState));
                    Assert.That(nodeStatus.WebConfigurationSupported, Is.EqualTo(result!.WebConfigurationSupported));
                    Assert.That(nodeStatus.DHCP_ConfigurationUsed, Is.EqualTo(result!.DHCP_ConfigurationUsed));
                    Assert.That(nodeStatus.DHCP_ConfigurationSupported, Is.EqualTo(result!.DHCP_ConfigurationSupported));
                    Assert.That(nodeStatus.PortAddressBitResolution, Is.EqualTo(result!.PortAddressBitResolution));
                    Assert.That(nodeStatus.NodeSupportArtNet_sACN_Switching, Is.EqualTo(result!.NodeSupportArtNet_sACN_Switching));
                    Assert.That(nodeStatus.Squawking, Is.EqualTo(result!.Squawking));
                    Assert.That(nodeStatus.NodeSupportOutputStyleSwitching, Is.EqualTo(result!.NodeSupportOutputStyleSwitching));
                    Assert.That(nodeStatus.NodeSupportRDM_Switching, Is.EqualTo(result!.NodeSupportRDM_Switching));
                    Assert.That(nodeStatus.NodeSupportSwitchingBetweenInputOutput, Is.EqualTo(result!.NodeSupportSwitchingBetweenInputOutput));
                    Assert.That(nodeStatus.NodeSupportLLRP, Is.EqualTo(result!.NodeSupportLLRP));
                    Assert.That(nodeStatus.NodeSupportFailOver, Is.EqualTo(result!.NodeSupportFailOver));
                    Assert.That(nodeStatus.FailsafeState, Is.EqualTo(result!.FailsafeState));
                    Assert.That(nodeStatus.GetHashCode(), Is.EqualTo(result!.GetHashCode()));
                    Assert.That(nodeStatus, Is.EqualTo(result));
                    Assert.That(nodeStatus == result, Is.True);
                    Assert.That(nodeStatus != result, Is.False);
                    Assert.That(nodeStatus.Equals((object)result), Is.True);
                    Assert.That(nodeStatus.Equals(null), Is.False);
                });
            }

            NodeStatus a = new NodeStatus(0b01010101, 0b10101010, 0b00001111);
            NodeStatus b = new NodeStatus(0b10101010, 0b01010101, 0b11110000);
            Assert.Multiple(() =>
            {
                Assert.That(a != b, Is.True);
                Assert.That(a == b, Is.False);
                Assert.That(a != ~b, Is.False);
                Assert.That(a == ~b, Is.True);
                Assert.That(~a != b, Is.False);
                Assert.That(~a == b, Is.True);
                Assert.That(~a != ~b, Is.True);
                Assert.That(~a == ~b, Is.False);
            });

            NodeStatus c = a & b;
            Assert.That(c, Is.EqualTo(NodeStatus.None));
            c = a & ~b;
            Assert.That(c, Is.EqualTo(a));
            c = ~a & b;
            Assert.That(c, Is.EqualTo(b));
        }
        [Test]
        public void TestGoodOutput()
        {
            HashSet<GoodOutput> subjects = new HashSet<GoodOutput>();
            for (int i = 0; i < 0xffffff; i += 0b01010101)
                subjects.Add(new GoodOutput((byte)(i & 0xff), (byte)((i >> 8) & 0xff)));

            subjects.Add(new GoodOutput(GoodOutput.EConvertFrom.sACN, EMergeMode.LTP, true, true, true, true, true, true, GoodOutput.EOutputStyle.Continuous, true));
            subjects.Add(new GoodOutput(GoodOutput.EConvertFrom.sACN, EMergeMode.HTP, true, false, false, true, true, true, GoodOutput.EOutputStyle.Continuous, true));
            subjects.Add(new GoodOutput(GoodOutput.EConvertFrom.ArtNet, EMergeMode.LTP, true, false, false, true, true, true, GoodOutput.EOutputStyle.Delta, true));

            foreach (GoodOutput goodOutput in subjects)
            {
                GoodOutput result = new GoodOutput(goodOutput.Byte1, goodOutput.Byte2);
                test();
                result = new GoodOutput(
                    goodOutput.ConvertFrom,
                    goodOutput.MergeMode,
                    goodOutput.DMX_OutputShortCircuit,
                    goodOutput.MergingArtNetData,
                    goodOutput.DMX_TextPacketsSupported,
                    goodOutput.DMX_SIPsSupported,
                    goodOutput.DMX_TestPacketsSupported,
                    goodOutput.IsBeingOutputAsDMX,
                    goodOutput.OutputStyle,
                    goodOutput.RDMisDisabled,
                    goodOutput.DiscoveryIsCurrentlyRunning,
                    goodOutput.BackgroundDiscoveryIsEnabled);
                test();
                void test()
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(goodOutput.Byte1, Is.EqualTo(result!.Byte1));
                        Assert.That(goodOutput.Byte2, Is.EqualTo(result!.Byte2));
                        Assert.That(goodOutput.ConvertFrom, Is.EqualTo(result!.ConvertFrom));
                        Assert.That(goodOutput.MergeMode, Is.EqualTo(result!.MergeMode));
                        Assert.That(goodOutput.DMX_OutputShortCircuit, Is.EqualTo(result!.DMX_OutputShortCircuit));
                        Assert.That(goodOutput.MergingArtNetData, Is.EqualTo(result!.MergingArtNetData));
                        Assert.That(goodOutput.DMX_TextPacketsSupported, Is.EqualTo(result!.DMX_TextPacketsSupported));
                        Assert.That(goodOutput.DMX_SIPsSupported, Is.EqualTo(result!.DMX_SIPsSupported));
                        Assert.That(goodOutput.DMX_TestPacketsSupported, Is.EqualTo(result!.DMX_TestPacketsSupported));
                        Assert.That(goodOutput.IsBeingOutputAsDMX, Is.EqualTo(result!.IsBeingOutputAsDMX));
                        Assert.That(goodOutput.OutputStyle, Is.EqualTo(result!.OutputStyle));
                        Assert.That(goodOutput.RDMisDisabled, Is.EqualTo(result!.RDMisDisabled));
                        Assert.That(goodOutput.GetHashCode(), Is.EqualTo(result!.GetHashCode()));
                        Assert.That(goodOutput, Is.EqualTo(result));
                        Assert.That(goodOutput == result, Is.True);
                        Assert.That(goodOutput != result, Is.False);
                        Assert.That(goodOutput.Equals((object)result), Is.True);
                        Assert.That(goodOutput.Equals(null), Is.False);
                    });
                }
            }

            GoodOutput a = new GoodOutput(0b01010101, 0b10101010);
            GoodOutput b = new GoodOutput(0b10101010, 0b01010101);
            Assert.Multiple(() =>
            {
                Assert.That(a != b, Is.True);
                Assert.That(a == b, Is.False);
                Assert.That(a != ~b, Is.False);
                Assert.That(a == ~b, Is.True);
                Assert.That(~a != b, Is.False);
                Assert.That(~a == b, Is.True);
                Assert.That(~a != ~b, Is.True);
                Assert.That(~a == ~b, Is.False);
            });

            ushort aUshort = (ushort)a;
            GoodOutput aFromUshort = (GoodOutput)aUshort;
            Assert.That(aFromUshort, Is.EqualTo(a));

            ushort bUshort = (ushort)b;
            GoodOutput bFromUshort = (GoodOutput)bUshort;
            Assert.That(bFromUshort, Is.EqualTo(b));



            GoodOutput c = a & b;
            Assert.That(c, Is.EqualTo(GoodOutput.None));
            c = a & ~b;
            Assert.That(c, Is.EqualTo(a));
            c = ~a & b;
            Assert.That(c, Is.EqualTo(b));

            a = new GoodOutput(0b00000000, 0b00001111);
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(convertFrom: GoodOutput.EConvertFrom.ArtNet);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(convertFrom: GoodOutput.EConvertFrom.sACN);
            Assert.That(a.Byte1, Is.EqualTo(0b00000001));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(mergeMode: EMergeMode.HTP);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(mergeMode: EMergeMode.LTP);
            Assert.That(a.Byte1, Is.EqualTo(0b00000010));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(dmx_OutputShortCircuit: false);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(dmx_OutputShortCircuit: true);
            Assert.That(a.Byte1, Is.EqualTo(0b00000100));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(mergingArtNetData: false);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(mergingArtNetData: true);
            Assert.That(a.Byte1, Is.EqualTo(0b00001000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(dmx_TextPacketsSupported: false);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(dmx_TextPacketsSupported: true);
            Assert.That(a.Byte1, Is.EqualTo(0b00010000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(dmx_SIPsSupported: false);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(dmx_SIPsSupported: true);
            Assert.That(a.Byte1, Is.EqualTo(0b00100000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(dmx_TestPacketsSupported: false);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(dmx_TestPacketsSupported: true);
            Assert.That(a.Byte1, Is.EqualTo(0b01000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(isBeingOutputAsDMX: false);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(isBeingOutputAsDMX: true);
            Assert.That(a.Byte1, Is.EqualTo(0b10000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));


            a = new GoodOutput(outputStyle: GoodOutput.EOutputStyle.Delta);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(outputStyle: GoodOutput.EOutputStyle.Continuous);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b01000000));

            a = new GoodOutput(rdmIsDisabled: false);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(rdmIsDisabled: true);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b10000000));

            a = new GoodOutput(discoveryIsCurrentlyRunning: false);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(discoveryIsCurrentlyRunning: true);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00100000));

            a = new GoodOutput(backgroundDiscoveryIsEnabled: false);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00000000));

            a = new GoodOutput(backgroundDiscoveryIsEnabled: true);
            Assert.That(a.Byte1, Is.EqualTo(0b00000000));
            Assert.That(a.Byte2, Is.EqualTo(0b00010000));
        }
        [Test]
        public void TestGoodInput()
        {
            HashSet<GoodInput> subjects = new HashSet<GoodInput>();
            for (byte i = 0; i < byte.MaxValue; i++)
                subjects.Add(new GoodInput(i));

            subjects.Add(new GoodInput(GoodInput.EConvertTo.sACN, true, true, true, true, true, true));
            subjects.Add(new GoodInput(GoodInput.EConvertTo.sACN, true, false, false, true, true, true));
            subjects.Add(new GoodInput(GoodInput.EConvertTo.ArtNet, true, false, false, true, true, true));

            foreach (GoodInput goodInput in subjects)
            {
                GoodInput result = new GoodInput(goodInput.Byte1);
                Assert.Multiple(() =>
                {
                    Assert.That(goodInput.Byte1, Is.EqualTo(result!.Byte1));
                    Assert.That(goodInput.ConvertTo, Is.EqualTo(result!.ConvertTo));
                    Assert.That(goodInput.ReceiveErrorsDetected, Is.EqualTo(result!.ReceiveErrorsDetected));
                    Assert.That(goodInput.InputDisabled, Is.EqualTo(result!.InputDisabled));
                    Assert.That(goodInput.DMX_TextPacketsSupported, Is.EqualTo(result!.DMX_TextPacketsSupported));
                    Assert.That(goodInput.DMX_SIPsSupported, Is.EqualTo(result!.DMX_SIPsSupported));
                    Assert.That(goodInput.DMX_TestPacketsSupported, Is.EqualTo(result!.DMX_TestPacketsSupported));
                    Assert.That(goodInput.DataReceived, Is.EqualTo(result!.DataReceived));
                    Assert.That(goodInput.GetHashCode(), Is.EqualTo(result!.GetHashCode()));
                    Assert.That(goodInput, Is.EqualTo(result));
                    Assert.That(goodInput == result, Is.True);
                    Assert.That(goodInput != result, Is.False);
                    Assert.That(goodInput.Equals((object)result), Is.True);
                    Assert.That(goodInput.Equals(null), Is.False);
                });
            }

            GoodInput a = new GoodInput(0b01010101);
            GoodInput b = new GoodInput(0b10101010);
            Assert.Multiple(() =>
            {
                Assert.That(a != b, Is.True);
                Assert.That(a == b, Is.False);
                Assert.That(a != ~b, Is.False);
                Assert.That(a == ~b, Is.True);
                Assert.That(~a != b, Is.False);
                Assert.That(~a == b, Is.True);
                Assert.That(~a != ~b, Is.True);
                Assert.That(~a == ~b, Is.False);
            });

            byte aByte = (byte)a;
            GoodInput aFromByte = (GoodInput)aByte;
            Assert.That(aFromByte, Is.EqualTo(a));

            byte bByte = (byte)b;
            GoodInput bFromByte = (GoodInput)bByte;
            Assert.That(bFromByte, Is.EqualTo(b));



            GoodInput c = a & b;
            Assert.That(c, Is.EqualTo(GoodInput.None));
            c = a & ~b;
            Assert.That(c, Is.EqualTo(a));
            c = ~a & b;
            Assert.That(c, Is.EqualTo(b));
        }
        [Test]
        public void TestRequestRDMMessageReceivedEventArgs()
        {
            var e = new RequestRDMMessageReceivedEventArgs(new RDMMessage() { Command = ERDM_Command.SET_COMMAND, Parameter = ERDM_Parameter.CURVE }, new PortAddress(123));

            Assert.That(e.Handled, Is.False);
            e.SetResponse(new RDMMessage() { Command = ERDM_Command.SET_COMMAND_RESPONSE, Parameter = ERDM_Parameter.CURVE });
            Assert.That(e.Handled, Is.True);
            e.SetResponse(new RDMMessage() { Command = ERDM_Command.SET_COMMAND_RESPONSE, Parameter = ERDM_Parameter.CURVE });
            Assert.That(e.Handled, Is.True);

            Assert.That(e.PortAddress, Is.EqualTo(new PortAddress(123)));
        }
        [Test]
        [Retry(2)]
        public async Task TestRDMUID_ReceivedBag()
        {
            var e = new RDMUID_ReceivedBag(new UID(123141));

            Assert.That(e.LastSeen.Date, Is.EqualTo(DateTime.UtcNow.Date));
            e.Seen();
            Assert.Multiple(() =>
            {
                Assert.That(e.LastSeen.Date, Is.EqualTo(DateTime.UtcNow.Date));
                Assert.That(e.Timouted(), Is.False);
            });
            byte number = e.NewTransactionNumber();
            Assert.That(e.TransactionNumber, Is.EqualTo(number));

            e.Seen();
            await Task.Delay(30500);
            Assert.That(e.Timouted(), Is.True);
        }
        [Test]
        [Retry(2)]
        public async Task TestControllerRDMUID_Bag()
        {
            var a = new ControllerRDMUID_Bag(new UID(123155541), new PortAddress(1, 2, 3), IPv4Address.LocalHost);
            var a2 = new ControllerRDMUID_Bag(new UID(123155541), new PortAddress(1, 2, 3), IPv4Address.LocalHost);
            var b = new ControllerRDMUID_Bag(new UID(1112), new PortAddress(1, 2, 3), IPv4Address.LocalHost);

            Assert.Multiple(() =>
            {
                Assert.That(a == b, Is.False);
                Assert.That(a != b, Is.True);
                Assert.That(a.Equals((object)b), Is.False);
                Assert.That(a.Equals(b), Is.False);
                Assert.That(a.Equals(null), Is.False);
                Assert.That(b.Equals((object)a!), Is.False);
                Assert.That(b.Equals(a), Is.False);
                Assert.That(b.Equals(null), Is.False);
                Assert.That(a!.GetHashCode(), Is.EqualTo(a2.GetHashCode()));
                Assert.That(b!.GetHashCode(), Is.Not.EqualTo(a.GetHashCode()));
            });

            var e = new ControllerRDMUID_Bag(new UID(123141), new PortAddress(1, 2, 3), IPv4Address.LocalHost);

            Assert.That(e.LastSeen.Date, Is.EqualTo(DateTime.UtcNow.Date));
            e.Seen();

            Assert.Multiple(() =>
            {
                Assert.That(e.LastSeen.Date, Is.EqualTo(DateTime.UtcNow.Date));
                Assert.That(e.Timouted(), Is.False);
            });

            e.Seen();
            await Task.Delay(30500);
            Assert.That(e.Timouted(), Is.True);
        }
        [Test]
        public async Task TestPortConfig()
        {
            HashSet<PortConfig> portConfigs = new HashSet<PortConfig>();
            for (byte i = 1; i < byte.MaxValue; i++)
            {
                bool _in = i % 2 == 0;
                bool _out = i % 3 == 0;
                var pa = new PortAddress((byte)(i & 0x7f), (Address)(byte)(i & 0x00ff));
                doTest(new PortConfig(i, pa.Address, _out, _in));
                doTest(new PortConfig(i, pa, _out, _in));
                doTest(new PortConfig(i, pa.Net, pa.Subnet, pa.Universe, _out, _in));
                doTest(new PortConfig(i, pa.Net, pa.Address, _out, _in));
                doTest(new PortConfig(i, pa.Subnet, pa.Universe, _out, _in));
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => new PortConfig(0, new PortAddress(1, 2, 3), false, false));
            var portConfig = new PortConfig(1, new PortAddress(1, 2, 3), true, false);
            portConfig.AddDiscoveredRdmUIDs(new UID(0x12345678));
            Assert.That(portConfig.GetReceivedRDMUIDs(), Has.Length.EqualTo(1));
            await Task.Delay(500);
            portConfig.AddDiscoveredRdmUIDs(new UID(0x12345678));
            Assert.That(portConfig.GetReceivedRDMUIDs(), Has.Length.EqualTo(1));
            await Task.Delay(500);
            portConfig.AddDiscoveredRdmUIDs(new UID(0xff345678));
            Assert.That(portConfig.GetReceivedRDMUIDs(), Has.Length.EqualTo(2));
            await Task.Delay(500);
            portConfig.AddDiscoveredRdmUIDs(new UID(0xff345678));
            Assert.That(portConfig.GetReceivedRDMUIDs(), Has.Length.EqualTo(2));
            await Task.Delay(29600);
            Assert.Multiple(() =>
            {
                Assert.That(portConfig.GetReceivedRDMUIDs(), Has.Length.EqualTo(1));
                Assert.That(portConfig.DiscoveredRDMUIDs, Has.Count.EqualTo(2));
            });
            portConfig.RemoveOutdatedRdmUIDs();
            Assert.That(portConfig.DiscoveredRDMUIDs, Has.Count.EqualTo(1));

            await Task.Delay(1000);
            Assert.Multiple(() =>
            {
                Assert.That(portConfig.GetReceivedRDMUIDs(), Has.Length.EqualTo(0));
                Assert.That(portConfig.DiscoveredRDMUIDs, Has.Count.EqualTo(1));
            });
            portConfig.RemoveOutdatedRdmUIDs();
            Assert.That(portConfig.DiscoveredRDMUIDs, Has.Count.EqualTo(0));

            portConfig.AddDiscoveredRdmUIDs([]);
            Assert.That(portConfig.DiscoveredRDMUIDs, Has.Count.EqualTo(0));

            bool received = false;
            portConfig.RDMUIDReceived += (o, e) => { received = true; };
            portConfig.AddDiscoveredRdmUIDs(new UID(0xff345678));
            Assert.That(received, Is.True);


            void doTest(PortConfig portConfig)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(portConfig, Is.Not.Null);
                    Assert.That(portConfig.ToString(), Is.Not.Null);
                    Assert.That(portConfig.ForceBroadcast, Is.False);
                    portConfig.ForceBroadcast = true;
                    Assert.That(portConfig.ForceBroadcast, Is.True);
                    portConfig.ForceBroadcast = false;
                    Assert.That(portConfig.ForceBroadcast, Is.False);
                    portConfigs.Add(portConfig);

                    Assert.That(portConfig.Address, Is.Not.Zero);
                    Assert.That(portConfig.Universe, Is.Not.Zero);
                    Assert.That(portConfig.Net, Is.Not.Zero);

                    Assert.That(portConfig.AdditionalIPEndpoints, Has.Count.EqualTo(0));
                    portConfig.AddAdditionalIPEndpoints([new IPv4Address("192.168.0.1"), new IPv4Address("192.168.0.2")]);
                    Assert.That(portConfig.AdditionalIPEndpoints, Has.Count.EqualTo(2));
                    portConfig.AddAdditionalIPEndpoints([new IPv4Address("192.168.0.3"), new IPv4Address("192.168.0.2")]);
                    Assert.That(portConfig.AdditionalIPEndpoints, Has.Count.EqualTo(3));
                    portConfig.RemoveAdditionalIPEndpoints([new IPv4Address("192.168.0.2")]);
                    Assert.That(portConfig.AdditionalIPEndpoints, Has.Count.EqualTo(2));
                    portConfig.ClearAdditionalIPEndpoints();
                    Assert.That(portConfig.AdditionalIPEndpoints, Has.Count.EqualTo(0));
                    portConfig.AddAdditionalIPEndpoints([]);
                    Assert.That(portConfig.AdditionalIPEndpoints, Has.Count.EqualTo(0));
                    portConfig.RemoveAdditionalIPEndpoints([new IPv4Address("192.168.0.2")]);
                    Assert.That(portConfig.AdditionalIPEndpoints, Has.Count.EqualTo(0));


                    Assert.That(portConfig.AdditionalRDMUIDs, Has.Count.EqualTo(0));
                    portConfig.AddAdditionalRdmUIDs([new UID(123456), new UID(22123456)]);
                    Assert.That(portConfig.AdditionalRDMUIDs, Has.Count.EqualTo(2));
                    portConfig.RemoveAdditionalRdmUIDs([new UID(22123456)]);
                    Assert.That(portConfig.AdditionalRDMUIDs, Has.Count.EqualTo(1));
                    portConfig.RemoveAdditionalRdmUIDs([new UID(123456)]);
                    Assert.That(portConfig.AdditionalRDMUIDs, Has.Count.EqualTo(0));
                    portConfig.AddAdditionalRdmUIDs([]);
                    Assert.That(portConfig.AdditionalRDMUIDs, Has.Count.EqualTo(0));
                    portConfig.RemoveAdditionalRdmUIDs([new UID(123456)]);
                    Assert.That(portConfig.AdditionalRDMUIDs, Has.Count.EqualTo(0));

                    Assert.That(portConfig.GetReceivedRDMUIDs(), Has.Length.EqualTo(0));
                });
            }
        }
        [Test]
        public void TestOutputPortConfig()
        {
            for (byte i = 1; i < byte.MaxValue; i++)
            {
                var pa = new PortAddress((byte)(i & 0x7f), (Address)(byte)(i & 0x00ff));
                doTest(new OutputPortConfig(i, pa.Address));
                doTest(new OutputPortConfig(i, pa));
                doTest(new OutputPortConfig(i, pa.Net, pa.Subnet, pa.Universe));
                doTest(new OutputPortConfig(i, pa.Net, pa.Address));
                doTest(new OutputPortConfig(i, pa.Subnet, pa.Universe));
            }
            void doTest(OutputPortConfig portConfig)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(portConfig.Type.HasFlag(EPortType.OutputFromArtNet), Is.True);
                    portConfig.Type = EPortType.DMX512;
                    Assert.That(portConfig.Type.HasFlag(EPortType.OutputFromArtNet), Is.True);
                    Assert.That(portConfig.Type.HasFlag(EPortType.DMX512), Is.True);

                    portConfig.Type = EPortType.DMX512 | EPortType.InputToArtNet;
                    Assert.That(portConfig.Type.HasFlag(EPortType.OutputFromArtNet), Is.True);
                    Assert.That(portConfig.Type.HasFlag(EPortType.DMX512), Is.True);
                    Assert.That(portConfig.Type.HasFlag(EPortType.InputToArtNet), Is.False);
                });
            }
        }
        [Test]
        public void TestInputPortConfig()
        {
            for (byte i = 1; i < byte.MaxValue; i++)
            {
                var pa = new PortAddress((byte)(i & 0x7f), (Address)(byte)(i & 0x00ff));
                doTest(new InputPortConfig(i, pa.Address));
                doTest(new InputPortConfig(i, pa));
                doTest(new InputPortConfig(i, pa.Net, pa.Subnet, pa.Universe));
                doTest(new InputPortConfig(i, pa.Net, pa.Address));
                doTest(new InputPortConfig(i, pa.Subnet, pa.Universe));
            }

            void doTest(InputPortConfig portConfig)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(portConfig.Type.HasFlag(EPortType.InputToArtNet), Is.True);
                    portConfig.Type = EPortType.DMX512;
                    Assert.That(portConfig.Type.HasFlag(EPortType.InputToArtNet), Is.True);
                    Assert.That(portConfig.Type.HasFlag(EPortType.DMX512), Is.True);
                    portConfig.Type = EPortType.DMX512 | EPortType.OutputFromArtNet;
                    Assert.That(portConfig.Type.HasFlag(EPortType.InputToArtNet), Is.True);
                    Assert.That(portConfig.Type.HasFlag(EPortType.DMX512), Is.True);
                    Assert.That(portConfig.Type.HasFlag(EPortType.OutputFromArtNet), Is.False);
                });
            }
        }
    }
}