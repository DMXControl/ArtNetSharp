using RDMSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ArtNetSharp
{
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public sealed class ArtPollReply : AbstractArtPacketCore
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    {
        public override EOpCodes OpCode => EOpCodes.OpPollReply;
        protected override ushort PacketMinLength => 198;
        protected override ushort PacketMaxLength => ushort.MaxValue;
        protected override ushort PacketBuildLength => 315;

        public readonly IPv4Address OwnIp;
        public readonly MACAddress MAC;
        public readonly IPv4Address BindIp;
        public readonly string ShortName;
        public readonly string LongName;
        public readonly ushort OemCode;
        /// <summary>
        /// The ESTA manufacturer code. The ESTA
        /// Manufacturer Code is assigned by ESTA and
        /// uniquely identifies the manufacturer.
        /// </summary>
        public readonly ushort ManufacturerCode;
        public NodeReport? NodeReport { get; private set; }
        public EPortType[] PortTypes { get; private set; }
        public EGoodInput[] GoodInput { get; private set; }
        public EGoodOutput[] GoodOutput { get; private set; }
        public EMacroState Macro { get; private set; }
        public ERemoteState Remote { get; private set; }
        public readonly byte BindIndex;
        public readonly byte MajorVersion;
        public readonly byte MinorVersion;
        public readonly byte Ports;
        public readonly ushort User;
        public readonly ushort RefreshRate;
        /// <summary>
        /// The top 7 bits of the 15 bit Port-Address to which this packet is destined.
        /// </summary>
        public readonly Net Net;
        public readonly Subnet Subnet;
        public readonly object[] OutputUniverses;
        public readonly object[] InputUniverses;
        public readonly byte UbeaVersion;
        public readonly NodeStatus Status;
        /// <summary>
        /// The sACN priority value that will be used when any received DMX is converted to sACN.
        /// </summary>
        public readonly byte AcnPriority;
        public readonly EStCodes Style;
        public readonly RDMUID? DefaulRespUID;

        private const byte MaxPortCount = 4;
        public ArtPollReply(in IPv4Address ownIp,
                            in IPv4Address bindIp,
                            in MACAddress mac,
                            in string shortName,
                            in string longName,
                            in byte bindIndex,
                            in NodeStatus status,
                            in byte majorVersion,
                            in byte minorVersion,
                            in Address outputUniverse,
                            in Address inputUniverse,
                            in ushort oemCode = Constants.DEFAULT_OEM_CODE,
                            in ushort manufacturerCode = Constants.DEFAULT_ESTA_MANUFACTURER_CODE,
                            in NodeReport? nodeReport = null,
                            in EPortType portType = default,
                            in EGoodInput goodInput = default,
                            in EGoodOutput goodOutput = default,
                            in EMacroState macro = EMacroState.None,
                            in ERemoteState remote = ERemoteState.None,
                            in byte ubeaVersion = 0,
                            in byte acnPriority = 0,
                            in ushort user = 0,
                            in ushort refreshRate = 0,
                            in EStCodes style = EStCodes.StController,
                            in RDMUID? defaulRespUID = null)
            : this(ownIp,
                  bindIp,
                  mac,
                  shortName,
                  longName,
                  bindIndex,
                  status & ~NodeStatus.NodeSupports15BitPortAddress,
                  majorVersion,
                  minorVersion,
                  0,
                  0,
                  new object[] { outputUniverse },
                  new object[] { inputUniverse },
                  oemCode,
                  manufacturerCode,
                  1,
                  nodeReport,
                  new EPortType[] { portType },
                  new EGoodInput[] { goodInput },
                  new EGoodOutput[] { goodOutput },
                  macro,
                  remote,
                  ubeaVersion,
                  acnPriority,
                  user,
                  refreshRate,
                  style,
                  defaulRespUID)
        {
        }
        public ArtPollReply(in IPv4Address ownIp,
                            in IPv4Address bindIp,
                            in MACAddress mac,
                            in string shortName,
                            in string longName,
                            in byte bindIndex,
                            in NodeStatus status,
                            in byte majorVersion,
                            in byte minorVersion,
                            in Net net,
                            in Subnet subNet,
                            in Universe outputUniverse,
                            in Universe inputUniverse,
                            in ushort oemCode = Constants.DEFAULT_OEM_CODE,
                            in ushort manufacturerCode = Constants.DEFAULT_ESTA_MANUFACTURER_CODE,
                            in NodeReport? nodeReport = null,
                            in EPortType portType = default,
                            in EGoodInput goodInput = default,
                            in EGoodOutput goodOutput = default,
                            in EMacroState macro = EMacroState.None,
                            in ERemoteState remote = ERemoteState.None,
                            in byte ubeaVersion = 0,
                            in byte acnPriority = 0,
                            in ushort user = 0,
                            in ushort refreshRate = 0,
                            in EStCodes style = EStCodes.StController,
                            in RDMUID? defaulRespUID = null)
            : this(ownIp,
                  bindIp,
                  mac,
                  shortName,
                  longName,
                  bindIndex,
                  status | NodeStatus.NodeSupports15BitPortAddress,
                  majorVersion,
                  minorVersion,
                  net,
                  subNet,
                  new object[] { outputUniverse },
                  new object[] { inputUniverse },
                  oemCode,
                  manufacturerCode,
                  1,
                  nodeReport,
                  new EPortType[] { portType },
                  new EGoodInput[] { goodInput },
                  new EGoodOutput[] { goodOutput },
                  macro,
                  remote,
                  ubeaVersion,
                  acnPriority,
                  user,
                  refreshRate,
                  style,
                  defaulRespUID)
        {
        }
        public ArtPollReply(in IPv4Address ownIp,
                        in IPv4Address bindIp,
                        in MACAddress mac,
                        in string shortName,
                        in string longName,
                        in byte bindIndex,
                        in NodeStatus status,
                        in byte majorVersion,
                        in byte minorVersion,
                        in Net net,
                        in Subnet subNet,
                        in object[] outputUniverses,
                        in object[] inputUniverses,
                        in ushort oemCode = Constants.DEFAULT_OEM_CODE,
                        in ushort manufacturerCode = Constants.DEFAULT_ESTA_MANUFACTURER_CODE,
                        in byte ports = 0,
                        in NodeReport? nodeReport = null,
                        in EPortType[] portTypes = null,
                        in EGoodInput[] goodInput = null,
                        in EGoodOutput[] goodOutput = null,
                        in EMacroState macro = EMacroState.None,
                        in ERemoteState remote = ERemoteState.None,
                        in byte ubeaVersion = 0,
                        in byte acnPriority = 0,
                        in ushort user = 0,
                        in ushort refreshRate = 0,
                        in EStCodes style = EStCodes.StController,
                        in RDMUID? defaulRespUID = null) : base()
        {
            if ((outputUniverses?.Length ?? 0) > MaxPortCount)
                throw new ArgumentOutOfRangeException($"The argument {nameof(outputUniverses)} should be an array with max size of {MaxPortCount}.");
            if ((inputUniverses?.Length ?? 0) > MaxPortCount)
                throw new ArgumentOutOfRangeException($"The argument {nameof(inputUniverses)} should be an array with max size of {MaxPortCount}.");
            if ((portTypes?.Length ?? 0) > MaxPortCount)
                throw new ArgumentOutOfRangeException($"The argument {nameof(portTypes)} should be an array with max size of {MaxPortCount}.");
            if ((goodInput?.Length ?? 0) > MaxPortCount)
                throw new ArgumentOutOfRangeException($"The argument {nameof(goodInput)} should be an array with max size of {MaxPortCount}.");
            if ((goodOutput?.Length ?? 0) > MaxPortCount)
                throw new ArgumentOutOfRangeException($"The argument {nameof(goodOutput)} should be an array with max size of {MaxPortCount}.");

            if (ports > MaxPortCount)
                throw new ArgumentOutOfRangeException($"The argument {nameof(ports)} should be an between 0 and {MaxPortCount}.");


            if (oemCode == 0)
                throw new ArgumentOutOfRangeException($"The argument {nameof(oemCode)} should be not 0x0000, insted use Constants.DEFAULT_OEM_CODE ({Constants.DEFAULT_OEM_CODE}).");
            if (manufacturerCode == 0)
                throw new ArgumentOutOfRangeException($"The argument {nameof(manufacturerCode)} should be not 0x0000, insted use Constants.DEFAULT_ESTA_MANUFACTURER_CODE ({Constants.DEFAULT_ESTA_MANUFACTURER_CODE}).");

            OwnIp = ownIp;
            MAC = mac;
            BindIp = bindIp;

            if (String.IsNullOrWhiteSpace(shortName))
                ShortName = string.Empty;
            else
                ShortName = shortName.Length > 18 ? shortName.Substring(0, 18) : shortName;

            if (String.IsNullOrWhiteSpace(longName))
                LongName = string.Empty;
            else
                LongName = longName.Length > 64 ? longName.Substring(0, 64) : longName;

            OemCode = oemCode;
            ManufacturerCode = manufacturerCode;
            Status = status;
            BindIndex = bindIndex;
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            Net = net;
            Subnet = subNet;
            OutputUniverses = outputUniverses;
            InputUniverses = inputUniverses;
            UbeaVersion = ubeaVersion;
            AcnPriority = acnPriority;
            Style = style;
            Ports = ports;
            NodeReport = nodeReport;
            PortTypes = portTypes;
            GoodInput = goodInput;
            GoodOutput = goodOutput;
            Macro = macro;
            Remote = remote;
            DefaulRespUID = defaulRespUID;
            User = user;
            RefreshRate = refreshRate;
        }
        public ArtPollReply(in byte[] packet) : base(packet)
        {
            int length = packet.Length;

            //3 IP Address[4]
            OwnIp = new IPv4Address(packet[10], packet[11], packet[12], packet[13]);
            // 4 Port (Not needet)

            MajorVersion = packet[16]; // 5 VersInfoH
            MinorVersion = packet[17]; // 6 VersInfoL
            Net = packet[18]; // 7 NetSwitch
            Subnet = packet[19]; // 8 SubSwitch
            OemCode = (ushort)(packet[20] << 8 | packet[21]); // 9 & 10 OEM code
            UbeaVersion = packet[22]; // 11 UbeaVersion
            byte status1 = packet[23]; // 12 Status 1
            byte status2 = 0;
            byte status3 = 0;
            if (length > 212) // 40 Status 2
                status2 = packet[212];
            if (length > 217) // 42 Status 3
                status3 = packet[217];
            Status = new NodeStatus(status1, status2, status3);
            if (Status.PortAddressBitResolution == NodeStatus.EPortAddressBitResolution._8Bit)
            {
                Net = 0;
                Subnet = 0;
            }
            ManufacturerCode = (ushort)(packet[25] << 8 | packet[24]); // 13 & 14 ESTA manufacturer
            ShortName = Encoding.ASCII.GetString(packet, 26, 18).Split('\0')[0]; // 15 ShortName [18]
            LongName = Encoding.ASCII.GetString(packet, 44, 64).Split('\0')[0]; // 16 LongName  [64]

            // 17 NodeReport [64]
            string nodeReport = Encoding.ASCII.GetString(packet, 108, 64);
            if (!string.IsNullOrWhiteSpace(nodeReport))
            {
                nodeReport = nodeReport.TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(nodeReport))
                    NodeReport = new NodeReport(nodeReport);
            }

            // 18 NumPortsHi
            Ports = packet[173]; // 19 NumPortsLo
            byte portCount = Math.Min(Ports, (byte)4);


            List<EPortType> portTypes = new List<EPortType>(portCount);
            List<EGoodInput> goodInput = new List<EGoodInput>(portCount);
            List<EGoodOutput> goodOutputA = new List<EGoodOutput>(portCount);
            List<EGoodOutput> goodOutputB = new List<EGoodOutput>(portCount);
            List<object> swIn = new List<object>(portCount);
            List<object> swOut = new List<object>(portCount);
            for (byte i = 0; i < portCount; i++)
            {
                // 20 PortTypes [4]
                EPortType _portType = default;
                if (length > 174 + i)
                    _portType = (EPortType)packet[174 + i];
                portTypes.Add(_portType);

                // 21 GoodInput [4]
                EGoodInput _goodInput = default;
                if (length > 178 + i)
                    _goodInput = (EGoodInput)packet[178 + i];
                goodInput.Add(_goodInput);

                // 22 GoodOutputA [4]
                EGoodOutput _goodOutputA = default;
                if (length > 182 + i)
                    _goodOutputA=(EGoodOutput)packet[182 + i];
                goodOutputA.Add(_goodOutputA);

                if (length > 186 + i) // 23 SwIn [4] Input Universe
                    swIn.Add(getUniverseOrAddress(packet[186 + i]));
                if (length > 190 + i) // 24 SwOut [4] Output Universe
                    swOut.Add(getUniverseOrAddress(packet[190 + i]));

                // 41 GoodOutputB [4]
                EGoodOutput _goodOutputB = default;
                if (length > 213 + i)
                    _goodOutputB = (EGoodOutput)(ushort)(packet[213 + i] << 8);
                goodOutputB.Add(_goodOutputB);
            }

            if (length > 194) // 25 AcnPriority
                AcnPriority = packet[194];
            if (length > 195) // 26 SwMacro
                Macro = (EMacroState)packet[195];
            if (length > 196) // 27 SwRemote
                Remote = (ERemoteState)packet[196];

            // 28 Spare
            // 29 Spare
            // 30 Spare

            if (length > 200) // 31 Style
                Style = (EStCodes)packet[200];

            if (/*length > 201 && */length >= 206) // 32-37 MAC
                MAC = new MACAddress(packet.Skip(201).Take(6).ToArray());

            if (/*length > 207 && */length >= 210) // 38 Bind IP
                BindIp = new IPv4Address(packet[207], packet[208], packet[209], packet[210]);

            if (length > 211) // 39 Bind Index
                BindIndex = packet[211];

            // 41 GoodOutputB [4] see below 22 GoodOutputA [4]

            if (length > 218) // 43-48 DefaulRespUID
            {
                byte[] buffer = new byte[8];
                for (int j = 0; j < 6; j++)
                    buffer[5 - j] = packet[218 + j];
                DefaulRespUID = new RDMUID(BitConverter.ToUInt64(buffer, 0));
            }

            if (length > 224) // 49 & 50 User
                User = (ushort)(packet[224] << 8 | packet[225]);

            if (length > 226) // 51 & 52 RefreshRate
                RefreshRate = (ushort)(packet[226] << 8 | packet[227]);

            // 53 Filler 11x8

            for (byte i = 0; i < portCount; i++)
            {
                if (length > 186 + i) // 23 SwIn [4] Input Universe
                    swIn.Add(getUniverseOrAddress(packet[186 + i]));
                if (length > 190 + i) // 24 SwOut [4] Output Universe
                    swOut.Add(getUniverseOrAddress(packet[190 + i]));
            }

            object getUniverseOrAddress(byte b)
            {
                if (Status.PortAddressBitResolution == NodeStatus.EPortAddressBitResolution._15Bit)
                    return (Universe)(b & 0xf);
                return (Address)b;
            }

            List<EGoodOutput> goodOutput = new List<EGoodOutput>(portCount);
            for (int i = 0; i < goodOutputA.Count; i++)
                goodOutput.Add(goodOutputA[i] | goodOutputB[i]);

            this.PortTypes = portTypes.Take(Ports).ToArray();
            this.GoodInput = goodInput.Take(Ports).ToArray();
            this.GoodOutput = goodOutput.Take(Ports).ToArray();
            this.InputUniverses = swIn.Take(Ports).ToArray();
            this.OutputUniverses = swOut.Take(Ports).ToArray();
        }

        protected sealed override void fillPacketCore(ref byte[] p)
        {
            //3 IP Address[4]
            p[10] = OwnIp.B1; // IP Address 1
            p[11] = OwnIp.B2; // IP Address 2
            p[12] = OwnIp.B3; // IP Address 3
            p[13] = OwnIp.B4; // IP Address 4

            // 4 Port
            Tools.FromUShort(Constants.ARTNET_PORT, out p[14], out p[15]);

            p[16] = MajorVersion;                                   // 5 VersInfoH
            p[17] = MinorVersion;                                   // 6 VersInfoL
            p[18] = Net;                                            // 7 NetSwitch
            p[19] = Subnet;                                         // 8 SubSwitch
            Tools.FromUShort(OemCode, out p[21], out p[20]);        // 9 & 10 OEM code
            p[22] = UbeaVersion;                                    // 11 UbeaVersion
            p[23] = Status.StatusByte1;//p[23] = 0xD0;    // 12 Status 1 - Indicator normal, addresses set by "front panel"
            Tools.FromUShort(ManufacturerCode, out p[24], out p[25]); // 13 & 14 ESTA manufacturer
            Encoding.ASCII.GetBytes(ShortName).CopyTo(p, 26);       // 15 ShortName [18]
            Encoding.ASCII.GetBytes(LongName).CopyTo(p, 44);        // 16 LongName  [64]

            // 17 NodeReport [64]
            if (NodeReport.HasValue)
                Encoding.ASCII.GetBytes(NodeReport.Value.ToString()).CopyTo(p, 108);

            p[172] = 0; // 18 NumPortsHi
            p[173] = Ports; // 19 NumPortsLo

            // 20 PortTypes [4]
            //p[174] = (byte)((IsOutput ? 0x80 : 0x0) | (IsInput ? 0x40 : 0x0) | 0x05); // Art-Net data
            if (PortTypes != null)
                for (int i = 0; i < PortTypes.Length; i++)
                    p[174 + i] = (byte)PortTypes[i];

            // 21 GoodInput [4]
            if (GoodInput != null)
                for (int i = 0; i < GoodInput.Length; i++)
                    p[178 + i] = (byte)GoodInput[i];

            // 22 GoodOutputA [4]
            //p[182] = (byte)(IsOutput ? 0x80 : 0x0);
            if (GoodOutput != null)
                for (int i = 0; i < GoodOutput.Length; i++)
                    p[182 + i] = (byte)((ushort)GoodOutput[i] & 0xff);

            // 23 SwIn [4] Input Universe
            //p[186] = ReceiveUniverse;
            if (InputUniverses != null)
                for (int i = 0; i < InputUniverses.Length; i++)
                    p[186 + i] = InputUniverses[i] is Universe ? (Universe)InputUniverses[i] : (Address)InputUniverses[i];

            // 24 SwIn [4] Input Universe
            //p[190] = SendUniverse;
            if (OutputUniverses != null)
                for (int i = 0; i < OutputUniverses.Length; i++)
                    p[190 + i] = OutputUniverses[i] is Universe ? (Universe)OutputUniverses[i] : (Address)OutputUniverses[i];

            p[194] = AcnPriority; // 25 AcnPriority
            p[195] = (byte)Macro; // 26 SwMacro
            p[196] = (byte)Remote; // 27 SwRemote

            p[197] = 0; // 28 Spare
            p[198] = 0; // 29 Spare
            p[199] = 0; // 30 Spare

            p[200] = (byte)Style; // 31 Style

            p[201] = MAC.B1; // 32 MAC 1
            p[202] = MAC.B2; // 33 MAC 2
            p[203] = MAC.B3; // 34 MAC 3
            p[204] = MAC.B4; // 35 MAC 4
            p[205] = MAC.B5; // 36 MAC 5
            p[206] = MAC.B6; // 37 MAC 6

            // 38 Bind IP
            p[207] = BindIp.B1; // Bind IP 1
            p[208] = BindIp.B2; // Bind IP 2
            p[209] = BindIp.B3; // Bind IP 3
            p[210] = BindIp.B4; // Bind IP 4

            p[211] = (byte)BindIndex; // 39 BindIndex (0 and 1 are equal)


            p[212] = Status.StatusByte2; // 40 Status 2

            // 41 GoodOutputB
            if (GoodOutput != null)
                for (int i = 0; i < GoodOutput.Length; i++)
                    p[213 + i] = (byte)((ushort)GoodOutput[i] >> 8);

            p[217] = Status.StatusByte3; // 42 Status 3

            // 43-48 DefaulRespUID
            if (DefaulRespUID.HasValue)
                Array.Copy(DefaulRespUID.Value.ToBytes().ToArray(), 0, p, 218, 6);

            Tools.FromUShort(User, out p[225], out p[224]); // 49 & 50 User
            Tools.FromUShort(RefreshRate, out p[227], out p[226]); // 51 & 52 RefreshRate

            // 53 Filler 11x8
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtPollReply other
                && IPv4Address.Equals(OwnIp, other.OwnIp)
                && MACAddress.Equals(MAC, other.MAC)
                && IPv4Address.Equals(BindIp, other.BindIp)
                && String.Equals(ShortName, other.ShortName)
                && String.Equals(LongName, other.LongName)
                && OemCode == other.OemCode
                && ManufacturerCode == other.ManufacturerCode
                && ArtNetSharp.NodeReport.Equals(NodeReport, other.NodeReport)
                && PortTypes.SequenceEqual(other.PortTypes)
                && GoodInput.SequenceEqual(other.GoodInput)
                && GoodOutput.SequenceEqual(other.GoodOutput)
                && Macro == other.Macro
                && Remote == other.Remote
                && BindIndex == other.BindIndex
                && Ports == other.Ports
                && MajorVersion == other.MajorVersion
                && MinorVersion == other.MinorVersion
                && User == other.User
                && RefreshRate == other.RefreshRate
                && Net.Equals(Net, other.Net)
                && Subnet.Equals(Subnet, other.Subnet)
                && OutputUniverses.SequenceEqual(other.OutputUniverses)
                && InputUniverses.SequenceEqual(other.InputUniverses)
                && UbeaVersion == other.UbeaVersion
                && Status == other.Status
                && AcnPriority == other.AcnPriority
                && Style == other.Style
                && RDMUID.Equals(DefaulRespUID, other.DefaulRespUID);
            ;
        }
        public override string ToString()
        {
            return $"{nameof(ArtPollReply)}: {LongName} - #{BindIndex} OEM:{OemCode:x4}, Manuf.:{ManufacturerCode:x4}";
        }
    }
}