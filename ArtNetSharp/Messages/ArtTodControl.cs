namespace ArtNetSharp
{
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public sealed class ArtTodControl : AbstractArtPacketNetAddressCommand<EArtTodControlCommand>
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    {
        public sealed override EOpCodes OpCode => EOpCodes.OpTodControl;
        protected sealed override ushort PacketMinLength => 24;
        protected sealed override ushort NetByte => 21;
        protected sealed override ushort CommandByte => 22;
        protected sealed override ushort AddressByte => 23;

        public ArtTodControl(in PortAddress portAddress,
                             in EArtTodControlCommand command = EArtTodControlCommand.AtcFlush,
                             in ushort protocolVersion = Constants.PROTOCOL_VERSION) : this(portAddress.Net, portAddress.Address, command, protocolVersion)
        {
        }
        public ArtTodControl(in Net net,
                             in Address address,
                             in EArtTodControlCommand command = EArtTodControlCommand.AtcFlush,
                             in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(net, address, command, protocolVersion)
        {
        }
        public ArtTodControl(in byte[] packet) : base(packet)
        {
        }
        protected sealed override void fillPacket(ref byte[] p)
        {
            base.fillPacket(ref p);

            //p[13] = 0; // Filler 2
            //p[14] = 0; // Spare 1
            //p[15] = 0; // Spare 2
            //p[16] = 0; // Spare 3
            //p[17] = 0; // Spare 4
            //p[18] = 0; // Spare 5
            //p[19] = 0; // Spare 6
            //p[20] = 0; // Spare 7
            //p[21] = 0; // Net (done by Abstract part)
            //p[22] = 0; // Command (done by Abstract part)
            //p[23] = 0; // Address (done by Abstract part)
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtTodControl other
                && Command == other.Command;
        }
        public override string ToString()
        {
            return $"{nameof(ArtTodControl)}: Command: {Command}, PortAddress: {PortAddress.Combined:x4}";
        }
    }
}