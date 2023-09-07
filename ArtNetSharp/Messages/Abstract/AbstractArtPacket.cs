namespace ArtNetSharp
{
    public abstract class AbstractArtPacket : AbstractArtPacketCore
    {
        public readonly ushort ProtocolVersion;

        public AbstractArtPacket(in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base()
        {
            ProtocolVersion = protocolVersion;
        }
        public AbstractArtPacket(in byte[] packet) : base(packet)
        {
            ProtocolVersion = (ushort)(packet[10] << 8 | packet[11]);
        }
        protected sealed override void fillPacketCore(ref byte[] p)
        {
            Tools.FromUShort(ProtocolVersion, out p[11], out p[10]);
            fillPacket(ref p);
        }
        protected abstract void fillPacket(ref byte[] p);

        public static implicit operator byte[](AbstractArtPacket abstractArtPacket)
        {
            return abstractArtPacket.GetPacket();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj) &&
                   obj is AbstractArtPacket other &&
                   ProtocolVersion == other.ProtocolVersion;
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = hashCode * -1521134295 + ProtocolVersion.GetHashCode();
            return hashCode;
        }
    }
}