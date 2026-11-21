using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ArtNetSharp
{
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public sealed class ArtAddress : AbstractArtPacket
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    {
        public override sealed EOpCodes OpCode => EOpCodes.OpAddress;
        protected override sealed ushort PacketMinLength => 107;

        private const int PortCount = 4;

        /// <summary>Net to program (7 bit), null = no change.</summary>
        public readonly Net? Net;
        /// <summary>Sub-Net to program, null = no change.</summary>
        public readonly Subnet? Subnet;
        public readonly ArtAddressCommand Command;
        public readonly byte BindIndex;
        public readonly string ShortName;
        public readonly string LongName;
        /// <summary>SwOut per port index 0-3 (always 4 entries), null = no change for that port.</summary>
        public readonly Universe?[] OutputUniverses;
        /// <summary>SwIn per port index 0-3 (always 4 entries), null = no change for that port.</summary>
        public readonly Universe?[] InputUniverses;
        public readonly byte? AcnPriority;

        /// <summary>Spec: the value 0x00 resets the field to the "physical switch setting" (for this library: the value the application configured).</summary>
        public readonly bool NetReset;
        public readonly bool SubnetReset;
        /// <summary>Reset flags per port index 0-3 (always 4 entries).</summary>
        public readonly bool[] OutputUniverseReset;
        public readonly bool[] InputUniverseReset;

        private static Universe?[] toPortArray(Universe?[] universes)
        {
            var result = new Universe?[PortCount];
            if (universes != null)
                Array.Copy(universes, result, Math.Min(universes.Length, PortCount));
            return result;
        }

        public ArtAddress(in byte bindIndex,
                      in Net? net,
                      in Subnet? subnet,
                      in Universe?[] outputUniverses,
                      in Universe?[] inputUniverses,
                      in string shortName,
                      in string longName,
                      in byte? acnPriority,
                      in ArtAddressCommand? command,
                      in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(protocolVersion)
        {


            BindIndex = bindIndex;
            Net = net;
            Subnet = subnet;
            ShortName = shortName;
            LongName = longName;
            // Fixed 4 entries, so the index always is the port index (as in the packet).
            OutputUniverses = toPortArray(outputUniverses);
            InputUniverses = toPortArray(inputUniverses);
            OutputUniverseReset = new bool[PortCount];
            InputUniverseReset = new bool[PortCount];
            Command = command ?? ArtAddressCommand.Default;

            if (acnPriority.HasValue && acnPriority.Value > 200)
            {
                if (acnPriority.Value.Equals(255))
                {
                    AcnPriority = null;
                    return;
                }
                throw new ArgumentOutOfRangeException($"{nameof(acnPriority)}");
            }
            else
                AcnPriority = acnPriority;
        }
        public ArtAddress(in byte[] packet) : base(packet)
        {
            // 5 NetSwitch: 7 bit Net, only programmed when bit 7 is set; 0x00 = reset (was masked to 4 bit before)
            if ((packet[12] & 0x80) == 0x80)
                Net = (byte)(packet[12] & 0x7f);
            NetReset = packet[12] == 0x00;

            BindIndex = packet[13];

            var shortName = Encoding.ASCII.GetString(packet, 14, 18).TrimEnd('\0'); // 7 ShortName [18]
            var longName = Encoding.ASCII.GetString(packet, 32, 64).TrimEnd('\0'); // 8 LongName  [64]
            if (!string.IsNullOrWhiteSpace(shortName))
                ShortName = shortName;
            if (!string.IsNullOrWhiteSpace(longName))
                LongName = longName;



            // 9 SwIn [4] / 10 SwOut [4]: one entry per port index; entries without bit 7 stay null (no change)
            // instead of being skipped (which shifted e.g. a change for port 2 onto port 1).
            InputUniverses = new Universe?[PortCount];
            OutputUniverses = new Universe?[PortCount];
            InputUniverseReset = new bool[PortCount];
            OutputUniverseReset = new bool[PortCount];
            for (int i = 0; i < PortCount; i++)
            {
                if ((packet[96 + i] & 0x80) == 0x80)
                    InputUniverses[i] = (Universe)(byte)(packet[96 + i] & 0x0f);
                InputUniverseReset[i] = packet[96 + i] == 0x00;

                if ((packet[100 + i] & 0x80) == 0x80)
                    OutputUniverses[i] = (Universe)(byte)(packet[100 + i] & 0x0f);
                OutputUniverseReset[i] = packet[100 + i] == 0x00;
            }

            if ((packet[104] & 0x80) == 0x80)
                Subnet = (byte)(packet[104] & 0x0f);
            SubnetReset = packet[104] == 0x00;

            AcnPriority = null;
            if (packet[105] <= 200)
                AcnPriority = packet[105];

            Command = packet[106];
        }
        public static ArtAddress CreateSetNet(in byte bindIndex, in Net net, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, net, null, null, null, null, null, null, command);
        }
        public static ArtAddress CreateSetSubnet(in byte bindIndex, in Subnet subnet, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, null, subnet, null, null, null, null, null, command);
        }
        public static ArtAddress CreateSetNetSubnet(in byte bindIndex, in Net net, in Subnet subnet, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, net, subnet, null, null, null, null, null, command);
        }
        public static ArtAddress CreateSetOutputUniverse(in byte bindIndex, in PortAddress outputPortAddress, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, outputPortAddress.Net, outputPortAddress.Subnet, new Universe?[] { outputPortAddress.Universe }, null, null, null, null, command);
        }
        public static ArtAddress CreateSetOutputUniverse(in byte bindIndex, in Net net, in Subnet subnet, in Universe outputUniverse, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, net, subnet, new Universe?[] { outputUniverse }, null, null, null, null, command);
        }
        public static ArtAddress CreateSetOutputUniverse(in byte bindIndex, in Universe outputUniverse, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, null, null, new Universe?[] { outputUniverse }, null, null, null, null, command);
        }
        public static ArtAddress CreateSetOutputUniverse(in byte bindIndex, in Net net, in Subnet subnet, in Universe?[] outputUniverses, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, net, subnet, outputUniverses, null, null, null, null, command);
        }
        public static ArtAddress CreateSetOutputUniverse(in byte bindIndex, in Universe?[] outputUniverses, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, null, null, outputUniverses, null, null, null, null, command);
        }
        public static ArtAddress CreateSetInputUniverse(in byte bindIndex, in PortAddress inputPortAddress, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, inputPortAddress.Net, inputPortAddress.Subnet, null, new Universe?[] { inputPortAddress.Universe }, null, null, null, command);
        }
        public static ArtAddress CreateSetInputUniverse(in byte bindIndex, in Net net, in Subnet subnet, in Universe inputUniverse, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, net, subnet, null, new Universe?[] { inputUniverse }, null, null, null, command);
        }
        public static ArtAddress CreateSetInputUniverse(in byte bindIndex, in Universe inputUniverse, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, null, null, null, new Universe?[] { inputUniverse }, null, null, null, command);
        }
        public static ArtAddress CreateSetInputUniverse(in byte bindIndex, in Net net, in Subnet subnet, in Universe?[] inputUniverses, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, net, subnet, null, inputUniverses, null, null, null, command);
        }
        public static ArtAddress CreateSetInputUniverse(in byte bindIndex, in Universe?[] inputUniverses, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, null, null, null, inputUniverses, null, null, null, command);
        }
        public static ArtAddress CreateSetName(in byte bindIndex, in string shortName = null, in string longName = null, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, null, null, null, null, shortName, longName, null, command);
        }
        public static ArtAddress CreateSetAcnPriority(in byte bindIndex, in byte acnPriority, ArtAddressCommand? command = null)
        {
            return new ArtAddress(bindIndex, null, null, null, null, null, null, acnPriority, command);
        }
        public static ArtAddress CreateSetCommand(in byte bindIndex, ArtAddressCommand command)
        {
            return new ArtAddress(bindIndex, null, null, null, null, null, null, null, command);
        }
        protected sealed override void fillPacket(ref byte[] p)
        {
            if (Net.HasValue)
                p[12] = (byte)(Net.Value | 0x80); // 5 Net (done by Abstract part)
            else
                p[12] = NetReset ? (byte)0x00 : (byte)0x7f;

            p[13] = (byte)BindIndex; // 6 BindIndex (0 and 1 are equal)

            if (!string.IsNullOrWhiteSpace(ShortName))
                Encoding.ASCII.GetBytes(ShortName).CopyTo(p, 14);       // 7 ShortName [18]
            if (!string.IsNullOrWhiteSpace(LongName))
                Encoding.ASCII.GetBytes(LongName).CopyTo(p, 32);        // 8 LongName  [64]

            // 9 SwIn [4] / 10 SwOut [4]: value | 0x80 = program, 0x00 = reset, 0x7f = no change
            for (int i = 0; i < PortCount; i++)
            {
                p[96 + i] = InputUniverses[i].HasValue ? (byte)(InputUniverses[i].Value | 0x80) : InputUniverseReset[i] ? (byte)0x00 : (byte)0x7f;
                p[100 + i] = OutputUniverses[i].HasValue ? (byte)(OutputUniverses[i].Value | 0x80) : OutputUniverseReset[i] ? (byte)0x00 : (byte)0x7f;
            }

            if (Subnet.HasValue)
                p[104] = (byte)(Subnet.Value | 0x80); // 11 Subnet (done by Abstract part)
            else
                p[104] = SubnetReset ? (byte)0x00 : (byte)0x7f;

            p[105] = byte.MaxValue;
            if (AcnPriority.HasValue)
                p[105] = AcnPriority.Value; // 12 AcnPriority

            p[106] = Command; // 13 Command (done by Abstract part)
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtAddress other
                && BindIndex == other.BindIndex
                && Net == other.Net
                && Subnet == other.Subnet
                && NetReset == other.NetReset
                && SubnetReset == other.SubnetReset
                && OutputUniverseReset.SequenceEqual(other.OutputUniverseReset)
                && InputUniverseReset.SequenceEqual(other.InputUniverseReset)
                && ShortName == other.ShortName
                && LongName == other.LongName
                && OutputUniverses.SequenceEqual(other.OutputUniverses)
                && InputUniverses.SequenceEqual(other.InputUniverses)
                && AcnPriority == other.AcnPriority
                && Command.Equals(other.Command);
        }

        public override string ToString()
        {
            return $"{nameof(ArtAddress)}: BindIndex: {BindIndex}";
        }
    }
}