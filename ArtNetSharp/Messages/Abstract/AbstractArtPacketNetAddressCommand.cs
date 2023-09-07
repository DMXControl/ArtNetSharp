namespace ArtNetSharp
{
    public abstract class AbstractArtPacketNetAddressCommand<T> : AbstractArtPacketNetAddress
    {
        protected abstract ushort CommandByte { get; }

        public readonly T Command;

        public AbstractArtPacketNetAddressCommand(in Net net,
                                           in Address address,
                                           in T command,
                                           in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(net, address, protocolVersion)
        {
            Command = command;
        }
        public AbstractArtPacketNetAddressCommand(in byte[] packet) : base(packet)
        {
            var b = packet[CommandByte];
            Command = (T)(object)b;
        }
        protected override void fillPacket(ref byte[] p)
        {
            base.fillPacket(ref p);
            p[CommandByte] = (byte)(object)Command;
        }

        public static implicit operator byte[](AbstractArtPacketNetAddressCommand<T> abstractArtPacketNetAddressCommand)
        {
            return abstractArtPacketNetAddressCommand.GetPacket();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj) &&
                   obj is AbstractArtPacketNetAddressCommand<T> other &&
                   Command.Equals(other.Command);
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = hashCode * -1521134295 + Command.GetHashCode();
            return hashCode;
        }
    }
}