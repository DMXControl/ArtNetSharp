using org.dmxc.wkdt.Light.RDM;
using System;
using System.Linq;

namespace ArtNetSharp
{
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public sealed class ArtRDMSub : AbstractArtPacket
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    {
        public override sealed EOpCodes OpCode => EOpCodes.OpRdmSub;
        protected override sealed ushort PacketMinLength => 32;
        protected override sealed ushort PacketMaxLength => ushort.MaxValue;
        protected override sealed ushort PacketBuildLength => (ushort)(PacketMinLength + Data.Length);

        public readonly byte[] Data;
        public readonly ERDMVersion RdmVersion;
        public readonly UID UID;
        public readonly byte CommandClass;
        public readonly ushort ParameterId;
        public readonly ushort SubDevice;
        public readonly ushort SubCount;

        public ArtRDMSub(in UID uid,
                      in byte commandClass,
                      in ushort parameterId,
                      in ushort subDevice,
                      in ushort subCount,
                      in byte[] data = default,
                      in ERDMVersion rdmVersion = ERDMVersion.STANDARD_V1_0,
                      in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(protocolVersion)
        {
            UID = uid;
            CommandClass = commandClass;
            ParameterId = parameterId;
            SubDevice = subDevice;
            SubCount = subCount;
            Data = data;
            RdmVersion = rdmVersion;
        }
        public ArtRDMSub(in byte[] packet) : base(packet)
        {
            RdmVersion = (ERDMVersion)packet[12];

            byte[] buffer = new byte[8];
            for (int j = 0; j < 6; j++)
                buffer[5 - j] = packet[14 + j];
            UID = new UID(BitConverter.ToUInt64(buffer, 0));

            CommandClass = packet[21];
            ParameterId = (ushort)(packet[22] << 8 | packet[23]);
            SubDevice = (ushort)(packet[24] << 8 | packet[25]);
            SubCount = (ushort)(packet[26] << 8 | packet[27]);

            Data = new byte[(packet.Length - 32)];
            Array.Copy(packet, 32, Data, 0, packet.Length - 32);
        }
        protected sealed override void fillPacket(ref byte[] p)
        {
            p[12] = (byte)RdmVersion;
            //p[13] = 0; // 6 Filler 2
            Array.Copy(UID.ToBytes().ToArray(), 0, p, 14, 6); // 7 UID
            //p[20] = 0; // 8 Spare 1
            p[21] = CommandClass; // 9 CommandClass
            Tools.FromUShort(ParameterId, out p[23], out p[22]); // 10 ParameterId
            Tools.FromUShort(SubDevice, out p[25], out p[24]); // 11 SubDevice
            Tools.FromUShort(SubCount, out p[27], out p[26]); // 11 SubCount
            //p[28] = 0; // 13 Spare 2
            //p[29] = 0; // 14 Spare 3
            //p[30] = 0; // 15 Spare 4
            //p[31] = 0; // 16 Spare 5
            Array.Copy(Data, 0, p, 32, Data.Length);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtRDMSub other
                && RdmVersion == other.RdmVersion
                && UID == other.UID
                && CommandClass == other.CommandClass
                && ParameterId == other.ParameterId
                && SubDevice == other.SubDevice
                && SubCount == other.SubCount
                && Data.SequenceEqual(other.Data);
        }
    }
}