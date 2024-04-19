using ArtNetSharp;
using ArtNetSharp.Misc;
using RDMSharp;

namespace ArtNetTests
{
    public class ObjectTypesTests
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
        public void TestUniverse()
        {
            HashSet<Universe> universes = new HashSet<Universe>();
            for (byte b = 0; b < byte.MaxValue; b++)
            {
                try
                {
                    Universe u = new Universe(b);

                    Assert.That(b, Is.LessThanOrEqualTo(0xf));
                    Assert.That(u.Value, Is.EqualTo(b));
                    Assert.That(u.ToString(), Is.Not.Empty);
                    universes.Add(u);
                }
                catch
                {
                    Assert.That(b, Is.GreaterThan(0xf));
                }
            }
            Assert.That(universes, Has.Count.EqualTo(0xf + 1));
            Assert.That(universes.OrderByDescending(s => s).OrderBy(s => s.GetHashCode()).OrderBy(s => s).ToList(), Has.Count.EqualTo(0xf + 1));

            Assert.That(new Universe(1) == (Universe)1, Is.True);
            Assert.That(new Universe(1) != (Universe)1, Is.False);
            Assert.That(new Universe(1) == (Universe)2, Is.False);
            Assert.That(new Universe(1) != (Universe)2, Is.True);
            Assert.That(new Universe(1).GetHashCode(), Is.EqualTo(((Universe)1).GetHashCode()));
            Assert.That(new Universe(1).GetHashCode(), Is.Not.EqualTo(((Universe)2).GetHashCode()));
            Assert.That(new Universe(1).Equals(null), Is.False);
            Assert.That(new Universe(1).Equals((object)1), Is.False);
            Assert.That(new Universe(1).Equals((object)(Universe)1), Is.True);
            Assert.That(new Universe(1).Equals((Universe)1), Is.True);
            Assert.That(new Universe(1).Equals((Universe)2), Is.False);
            Assert.That(new Universe(0).Equals(Universe.Default), Is.True);
        }

        [Test]
        public void TestSubnet()
        {
            HashSet<Subnet> subNets = new HashSet<Subnet>();
            for (byte b = 0; b < byte.MaxValue; b++)
            {
                try
                {
                    Subnet s = new Subnet(b);

                    Assert.That(b, Is.LessThanOrEqualTo(0xf));
                    Assert.That(s.Value, Is.EqualTo(b));
                    Assert.That(s.ToString(), Is.Not.Empty);
                    subNets.Add(s);
                }
                catch
                {
                    Assert.That(b, Is.GreaterThan(0xf));
                }
            }
            Assert.That(subNets, Has.Count.EqualTo(0xf + 1));
            Assert.That(subNets.OrderByDescending(s => s).OrderBy(s => s.GetHashCode()).OrderBy(s => s).ToList(), Has.Count.EqualTo(0xf + 1));

            Assert.That(new Subnet(1) == (Subnet)1, Is.True);
            Assert.That(new Subnet(1) != (Subnet)1, Is.False);
            Assert.That(new Subnet(1) == (Subnet)2, Is.False);
            Assert.That(new Subnet(1) != (Subnet)2, Is.True);
            Assert.That(new Subnet(1).GetHashCode(), Is.EqualTo(((Subnet)1).GetHashCode()));
            Assert.That(new Subnet(1).GetHashCode(), Is.Not.EqualTo(((Subnet)2).GetHashCode()));
            Assert.That(new Subnet(1).Equals(null), Is.False);
            Assert.That(new Subnet(1).Equals((object)1), Is.False);
            Assert.That(new Subnet(1).Equals((object)(Subnet)1), Is.True);
            Assert.That(new Subnet(1).Equals((Subnet)1), Is.True);
            Assert.That(new Subnet(1).Equals((Subnet)2), Is.False);
            Assert.That(new Subnet(0).Equals(Subnet.Default), Is.True);
        }

        [Test]
        public void TestAddress()
        {
            Address a = new Address(1);
            Assert.That(a.Universe.Value, Is.EqualTo(1));
            Assert.That(a.Combined, Is.EqualTo(1));

            a = new Address(15);
            Assert.That(a.Universe.Value, Is.EqualTo(15));
            Assert.That(a.Combined, Is.EqualTo(15));

            a = new Address(16);
            Assert.That(a.Universe.Value, Is.EqualTo(0));
            Assert.That(a.Subnet.Value, Is.EqualTo(1));
            Assert.That(a.Combined, Is.EqualTo(16));

            a = new Address(0xff);
            Assert.That(a.Universe.Value, Is.EqualTo(15));
            Assert.That(a.Subnet.Value, Is.EqualTo(15));
            Assert.That(a.Combined, Is.EqualTo(0xff));
            Assert.That(new Address(16) == new Address(1, 0));
            Assert.That(new Address(16) != new Address(0, 1));
            Assert.That(new Address(16), Is.EqualTo(new Address(1, 0)));
            Assert.That(new Address(16), Is.Not.EqualTo(new Address(0, 1)));
            Assert.That(new Address(16), Is.EqualTo((object)new Address(1, 0)));
            Assert.That(new Address(16).GetHashCode(), Is.Not.EqualTo(new Address(0, 1).GetHashCode()));
            Assert.That(new Address(16).Equals((object)new Address(1, 0)), Is.True);
            Assert.That(new Address(16).Equals((object)new Address(0, 1)), Is.False);
            Assert.That(new Address(16).Equals(null), Is.False);
            Assert.That(new Address(16), Is.Not.EqualTo(null));
            Assert.That(new Address(16).ToString(), Is.Not.Empty);

            HashSet<Address> addresses = new HashSet<Address>();
            for (byte b = 0; b < byte.MaxValue; b++)
                addresses.Add(new Address(b));
            Assert.That(addresses, Has.Count.EqualTo(byte.MaxValue));

            Assert.That(addresses.OrderByDescending(s=>s.Universe).ThenBy(s=>s).ToArray(), Has.Length.EqualTo(byte.MaxValue));
        }

        [Test]
        public void TestNet()
        {
            HashSet<Net> nets = new HashSet<Net>();
            for (byte b = 0; b < byte.MaxValue; b++)
            {
                try
                {
                    Net n = new Net(b);

                    Assert.That(b, Is.LessThanOrEqualTo(0x7f));
                    Assert.That(n.Value, Is.EqualTo(b));
                    Assert.That(n.ToString(), Is.Not.Empty);
                    nets.Add(n);
                }
                catch
                {
                    Assert.That(b, Is.GreaterThan(0x7f));
                }
            }
            Assert.That(nets, Has.Count.EqualTo(0x7f + 1));
            Assert.That(nets.OrderByDescending(s => s).OrderBy(s => s.GetHashCode()).OrderBy(s => s).ToList(), Has.Count.EqualTo(0x7f + 1));

            Assert.That(new Net(1) == (Net)1, Is.True);
            Assert.That(new Net(1) != (Net)1, Is.False);
            Assert.That(new Net(1) == (Net)2, Is.False);
            Assert.That(new Net(1) != (Net)2, Is.True);
            Assert.That(new Net(1).GetHashCode(), Is.EqualTo(((Net)1).GetHashCode()));
            Assert.That(new Net(1).GetHashCode(), Is.Not.EqualTo(((Net)2).GetHashCode()));
            Assert.That(new Net(1).Equals(null), Is.False);
            Assert.That(new Net(1).Equals((object)1), Is.False);
            Assert.That(new Net(1).Equals((object)(Net)1), Is.True);
            Assert.That(new Net(1).Equals((Net)1), Is.True);
            Assert.That(new Net(1).Equals((Net)2), Is.False);
        }
        [Test]
        public void TestPortAddress()
        {
            HashSet<PortAddress> portAddresses = new HashSet<PortAddress>();
            ushort count = 0;
            for (ushort b = 0; b < ushort.MaxValue; b++)
            {
                try
                {
                    PortAddress pa = new PortAddress(b);

                    Assert.That(b, Is.LessThanOrEqualTo(0x7fff));
                    Assert.That(pa.Combined, Is.EqualTo(b));
                    Assert.That(pa.ToString(), Is.Not.Empty);
                    portAddresses.Add(pa);
                    count++;
                }
                catch
                {
                    Assert.That(b, Is.GreaterThan(0x7fff));
                }
            }
            Assert.That(portAddresses, Has.Count.EqualTo(count));
            Assert.That(portAddresses.OrderByDescending(s => s).OrderBy(s => s.GetHashCode()).OrderBy(s => s).ToList(), Has.Count.EqualTo(count));

            Assert.That(new PortAddress((ushort)1) == new PortAddress(0, 0, 1), Is.True);
            Assert.That(new PortAddress((ushort)1) != new PortAddress(0, 1), Is.False);
            Assert.That(new PortAddress(1) == new PortAddress(0, 0, 2), Is.False);
            Assert.That(new PortAddress(1) != new PortAddress(0, 2), Is.True);
            Assert.That(new PortAddress(1).GetHashCode(), Is.EqualTo(((PortAddress)1).GetHashCode()));
            Assert.That(new PortAddress((ushort)1).GetHashCode(), Is.Not.EqualTo(((PortAddress)2).GetHashCode()));
            Assert.That(new PortAddress(1).Equals(null), Is.False);
            Assert.That(new PortAddress(1).Equals((object)1), Is.False);
            Assert.That(new PortAddress(1).Equals((object)(PortAddress)1), Is.True);
            Assert.That(new PortAddress(1).Equals((PortAddress)1), Is.True);
            Assert.That(new PortAddress(1).Equals((PortAddress)2), Is.False);
        }

        [Test]
        public void TestNodeReport()
        {
            HashSet<NodeReport> nodeReports= new HashSet<NodeReport>();
            NodeReport src = new NodeReport(ENodeReportCodes.RcFirmwareFail, "FAILED", 337);
            NodeReport dest = new NodeReport(src.ToString());
            Assert.That(dest, Is.EqualTo(src));
            Assert.That(dest.Equals(src), Is.True);
            Assert.That(dest.Equals((object)src), Is.True);
            Assert.That(dest.Equals(null), Is.False);

            src = new NodeReport(ENodeReportCodes.RcDebug, "Test", 0);
            for (byte i = 0; i < byte.MaxValue; i++)
            {
                src = src.Increment();
                Assert.That(dest, Is.Not.EqualTo(src));
                Assert.That(dest.Equals(src), Is.False);
                nodeReports.Add(src);
            }
            Assert.That(nodeReports.OrderBy(n=>n).ToList(),Has.Count.EqualTo(byte.MaxValue));

            Assert.DoesNotThrow(() => dest = new NodeReport(null));
        }
        [Test]
        public void TestIPv4Address()
        {
            IPv4Address src = new IPv4Address("192.168.178.158");
            IPv4Address dest = new IPv4Address(src.ToString());
            Assert.That(dest, Is.EqualTo(src));
        }
        [Test]
        public void TestMACAddress()
        {
            MACAddress src = new MACAddress("00:11:ee:44:2f:27");
            MACAddress dest = new MACAddress((byte[])src);
            Assert.That(dest, Is.EqualTo(src));
        }

        [Test]
        public void TestArtAddressCommand()
        {
            EArtAddressCommand[] commandsWithPort = {
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
                EArtAddressCommand.StyleDelta};

            EArtAddressCommand[] commandsWithoutPort = {
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
                Assert.That(artAddressCommand.Command, Is.EqualTo(command), state);
                Assert.That(artAddressCommand.Port, Is.EqualTo(port), state);
                byte serialized = (byte)artAddressCommand;
                ArtAddressCommand artAddressCommandResult = new ArtAddressCommand(serialized);
                Assert.That(artAddressCommandResult.Command, Is.EqualTo(command), state);
                Assert.That(artAddressCommandResult.Port, Is.EqualTo(port), state);
                Assert.That(artAddressCommandResult.GetHashCode(), Is.EqualTo(artAddressCommand.GetHashCode()), state);
                Assert.That(artAddressCommandResult, Is.EqualTo(artAddressCommand), state);
                Assert.That(artAddressCommandResult == artAddressCommand, Is.True, state);
                Assert.That(artAddressCommandResult != artAddressCommand, Is.False, state);
                Assert.That(artAddressCommandResult.Equals((object)artAddressCommand), Is.True, state);
                Assert.That(artAddressCommandResult.Equals(artAddressCommand), Is.True, state);
                Assert.That(artAddressCommandResult.Equals(null), Is.False, state);
                artAddressCommands.Add(artAddressCommand);
            }

            foreach (EArtAddressCommand command in commandsWithPort)
                for (byte port = 0; port < 4; port++)
                    doTests(command, port);
            foreach (EArtAddressCommand command in commandsWithoutPort)
                doTests(command, null);

            Assert.That(artAddressCommands, Has.Count.EqualTo((commandsWithPort.Length * 4) + commandsWithoutPort.Length));

            Assert.Throws(typeof(ArgumentException), () => new ArtAddressCommand(EArtAddressCommand.MergeHtp, null));
            Assert.Throws(typeof(ArgumentOutOfRangeException), () => new ArtAddressCommand(EArtAddressCommand.MergeHtp, 6));

            Assert.That(new ArtAddressCommand(EArtAddressCommand.MergeHtp, 1), Is.Not.EqualTo(new ArtAddressCommand(EArtAddressCommand.MergeHtp, 2)));
            Assert.That(new ArtAddressCommand(EArtAddressCommand.MergeLtp, 1), Is.Not.EqualTo(new ArtAddressCommand(EArtAddressCommand.MergeHtp, 2)));
            Assert.That(new ArtAddressCommand(EArtAddressCommand.MergeLtp, 1), Is.Not.EqualTo(new ArtAddressCommand(EArtAddressCommand.MergeHtp, 1)));
            Assert.That(ArtAddressCommand.Default, Is.EqualTo(new ArtAddressCommand(EArtAddressCommand.None, null)));
            Assert.That(ArtAddressCommand.Default.ToString(), Is.Not.Empty);
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
            }

            NodeStatus a = new NodeStatus(0b01010101, 0b10101010, 0b00001111);
            NodeStatus b = new NodeStatus(0b10101010, 0b01010101, 0b11110000);

            Assert.That(a != b, Is.True);
            Assert.That(a == b, Is.False);
            Assert.That(a != ~b, Is.False);
            Assert.That(a == ~b, Is.True);
            Assert.That(~a != b, Is.False);
            Assert.That(~a == b, Is.True);
            Assert.That(~a != ~b, Is.True);
            Assert.That(~a == ~b, Is.False);

            NodeStatus c = a & b;
            Assert.That(c, Is.EqualTo(NodeStatus.None));
            c = a & ~b;
            Assert.That(c, Is.EqualTo(a));
            c = ~a & b;
            Assert.That(c, Is.EqualTo(b));
        }

        [Test]
        public void TestControllerRDMMessageReceivedEventArgs()
        {
            var e = new ControllerRDMMessageReceivedEventArgs(new RDMMessage() { Command = ERDM_Command.SET_COMMAND, Parameter = ERDM_Parameter.CURVE });

            Assert.That(e.Handled, Is.False);
            e.SetResponse(new RDMMessage() { Command = ERDM_Command.SET_COMMAND_RESPONSE, Parameter = ERDM_Parameter.CURVE });
            Assert.That(e.Handled, Is.True);
            e.SetResponse(new RDMMessage() { Command = ERDM_Command.SET_COMMAND_RESPONSE, Parameter = ERDM_Parameter.CURVE });
            Assert.That(e.Handled, Is.True);
        }
    }
}