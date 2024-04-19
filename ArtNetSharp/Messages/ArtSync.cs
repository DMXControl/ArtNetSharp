namespace ArtNetSharp
{
    public sealed class ArtSync : AbstractArtPacket
    {
        public override sealed EOpCodes OpCode => EOpCodes.OpSync;
        protected override sealed ushort PacketMinLength => 14;
        protected override sealed ushort PacketMaxLength => 16;

        public ArtSync(in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(protocolVersion)
        {
        }
        public ArtSync(in byte[] packet) : base(packet)
        {
        }

        protected sealed override void fillPacket(ref byte[] packet)
        {
            //p[14] = 0; // Aux 1
            //p[15] = 0; // Aux 2
        }
        public override string ToString()
        {
            return $"{nameof(ArtSync)}";
        }
    }
}