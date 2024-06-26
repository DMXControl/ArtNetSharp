﻿namespace ArtNetSharp
{
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public sealed class ArtPoll : AbstractArtPacket
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    {
        public override sealed EOpCodes OpCode => EOpCodes.OpPoll;
        protected override sealed ushort PacketMinLength => 12;
        protected override sealed ushort PacketMaxLength => 22;

        public readonly EArtPollFlags Flags;
        public readonly EPriorityCode Priority;
        public readonly PortAddress TargetPortTop;
        public readonly PortAddress TargetPortBottom;
        public readonly ushort OemCode;
        /// <summary>
        /// The ESTA manufacturer code. The ESTA
        /// Manufacturer Code is assigned by ESTA and
        /// uniquely identifies the manufacturer.
        /// </summary>
        public readonly ushort ManufacturerCode;

        public ArtPoll(in ushort oemCode = Constants.DEFAULT_OEM_CODE,
                       in ushort manufacturerCode = Constants.DEFAULT_ESTA_MANUFACTURER_CODE,
                       in PortAddress targetPortTop = default,
                       in PortAddress targetPortBottom = default,
                       in EArtPollFlags flags = EArtPollFlags.None,
                       in EPriorityCode priority = EPriorityCode.None,
                       in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(protocolVersion)
        {
            OemCode = oemCode;
            ManufacturerCode = manufacturerCode;
            TargetPortTop = targetPortTop;
            TargetPortBottom = targetPortBottom;
            if (TargetPortBottom != default || TargetPortTop != default)
                Flags = flags | EArtPollFlags.EnableTargetedMode;
            else
                Flags = flags;
            Priority = priority;
        }
        public ArtPoll(in byte[] packet) : base(packet)
        {
            if (packet.Length >= 13)
                Flags = (EArtPollFlags)packet[12];
            if (packet.Length >= 14)
                Priority = (EPriorityCode)packet[13];
            if (packet.Length >= 16)
                TargetPortTop = (ushort)(packet[14] << 8 | packet[15]);
            if (packet.Length >= 18)
                TargetPortBottom = (ushort)(packet[16] << 8 | packet[17]);
            if (packet.Length >= 20)
                ManufacturerCode = (ushort)(packet[18] << 8 | packet[19]);
            if (packet.Length >= 22)
                OemCode = (ushort)(packet[20] << 8 | packet[21]);
        }

        protected sealed override void fillPacket(ref byte[] p)
        {
            p[12] = (byte)Flags; // Flags
            p[13] = (byte)Priority; // Priority

            Tools.FromUShort(TargetPortTop, out p[15], out p[14]);// TargetPortTop
            Tools.FromUShort(TargetPortBottom, out p[17], out p[16]);// TargetPortBottom

            Tools.FromUShort(ManufacturerCode, out p[19], out p[18]);// manufacturer code
            Tools.FromUShort(OemCode, out p[21], out p[20]);// OEM code
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtPoll other
                && ManufacturerCode == other.ManufacturerCode
                && OemCode == other.OemCode
                && TargetPortTop == other.TargetPortTop
                && TargetPortBottom == other.TargetPortBottom
                && Flags == other.Flags
                && Priority == other.Priority;
        }

        public override string ToString()
        {
            return $"{nameof(ArtPoll)}: OEM:{OemCode:x4}, Manuf.:{ManufacturerCode:x4}, Version:{ProtocolVersion}, TargetPortTop:{TargetPortTop}, TargetPortBottom:{TargetPortBottom}";
        }
    }
}