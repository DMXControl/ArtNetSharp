namespace ArtNetSharp
{
    public interface IArtPacketWithCommand<in T> where T : notnull
    {
        byte[] GetPacket();
    }
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public abstract class AbstractArtPacketNetAddressCommand<T> : AbstractArtPacketNetAddress, IArtPacketWithCommand<T>
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
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

        public override bool Equals(object obj)
        {
            return base.Equals(obj) &&
                   obj is AbstractArtPacketNetAddressCommand<T> other &&
                   Command.Equals(other.Command);
        }
    }
}