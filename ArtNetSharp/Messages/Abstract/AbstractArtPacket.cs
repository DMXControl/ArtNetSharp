namespace ArtNetSharp
{
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public abstract class AbstractArtPacket : AbstractArtPacketCore
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
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
    }
}