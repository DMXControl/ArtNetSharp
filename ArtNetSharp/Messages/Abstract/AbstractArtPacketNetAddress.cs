namespace ArtNetSharp
{
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public abstract class AbstractArtPacketNetAddress : AbstractArtPacketNet
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    {
        protected abstract ushort AddressByte { get; }
        /// <summary>
        /// The lowbyte of the 15 bit Port-Address to which this packet is destined.
        /// </summary>
        public readonly Address Address;
        public readonly PortAddress PortAddress;

        public AbstractArtPacketNetAddress(in Net net,
                             in Address address,
                             in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(net, protocolVersion)
        {
            Address = address;
        }
        public AbstractArtPacketNetAddress(in byte[] packet) : base(packet)
        {
            Address = packet[AddressByte];
            PortAddress = new PortAddress(Net, Address);
        }
        protected override void fillPacket(ref byte[] p)
        {
            base.fillPacket(ref p);
            p[AddressByte] = Address;
        }

        public static implicit operator byte[](AbstractArtPacketNetAddress abstractArtPacketNetAddress)
        {
            return abstractArtPacketNetAddress.GetPacket();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj) &&
                   obj is AbstractArtPacketNetAddress other &&
                   Address == other.Address;
        }
    }
}