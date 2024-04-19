namespace ArtNetSharp
{
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public sealed class ArtData : AbstractArtPacket
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    {
        public override sealed EOpCodes OpCode => EOpCodes.OpDataRequest;
        protected override sealed ushort PacketMinLength => 40;

        public readonly EDataRequest Request;
        public readonly ushort OemCode;
        /// <summary>
        /// The ESTA manufacturer code. The ESTA
        /// Manufacturer Code is assigned by ESTA and
        /// uniquely identifies the manufacturer.
        /// </summary>
        public readonly ushort ManufacturerCode;

        public ArtData(in ushort oemCode = Constants.DEFAULT_OEM_CODE,
                       in ushort manufacturerCode = Constants.DEFAULT_ESTA_MANUFACTURER_CODE,
                       in EDataRequest request = EDataRequest.Poll,
                       in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(protocolVersion)
        {
            OemCode = oemCode;
            ManufacturerCode = manufacturerCode;
            Request = request;
        }
        public ArtData(in byte[] packet) : base(packet)
        {
            ManufacturerCode = (ushort)(packet[12] << 8 | packet[13]);
            OemCode = (ushort)(packet[14] << 8 | packet[15]);
            Request = (EDataRequest)(ushort)(packet[16] << 8 | packet[17]);
            // Spare 22 byte
        }

        protected sealed override void fillPacket(ref byte[] p)
        {
            Tools.FromUShort(ManufacturerCode, out p[13], out p[12]);// manufacturer code
            Tools.FromUShort(OemCode, out p[15], out p[14]);// OEM code
            Tools.FromUShort((ushort)Request, out p[17], out p[16]);// Request
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtData other
                && ManufacturerCode == other.ManufacturerCode
                && OemCode == other.OemCode
                && Request == other.Request;
        }

        public override string ToString()
        {
            return $"{nameof(ArtData)}: Request:{Request}, OEM:{OemCode:x4}, Manuf.:{ManufacturerCode:x4}";
        }
    }
}