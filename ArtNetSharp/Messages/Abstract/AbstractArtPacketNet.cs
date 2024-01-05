namespace ArtNetSharp
{
    public abstract class AbstractArtPacketNet : AbstractArtPacket
    {
        protected abstract ushort NetByte { get; }
        /// <summary>
        /// The top 7 bits of the 15 bit Port-Address to which this packet is destined.
        /// </summary>
        public readonly Net Net;

        public AbstractArtPacketNet(in Net net,
                             in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(protocolVersion)
        {
            Net = net;
        }
        public AbstractArtPacketNet(in byte[] packet) : base(packet)
        {
            Net = (byte)(packet[NetByte] & 0x7F);
        }
        protected override void fillPacket(ref byte[] p)
        {
            p[NetByte] = (byte)(Net & 0x7f);
        }

        public static implicit operator byte[](AbstractArtPacketNet abstractArtPacketNet)
        {
            return abstractArtPacketNet.GetPacket();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj) &&
                   obj is AbstractArtPacketNet other &&
                   Net == other.Net;
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = hashCode * -1521134295 + Net.GetHashCode();
            return hashCode;
        }
    }
}