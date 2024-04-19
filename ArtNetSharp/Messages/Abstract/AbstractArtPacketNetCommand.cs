namespace ArtNetSharp
{
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public abstract class AbstractArtPacketNetCommand<T> : AbstractArtPacketNet, IArtPacketWithCommand<T>
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    {
        protected abstract ushort CommandByte { get; }

        public readonly T Command;

        public AbstractArtPacketNetCommand(in Net net,
                                           in T command,
                                           in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(net, protocolVersion)
        {
            Command = command;
        }
        public AbstractArtPacketNetCommand(in byte[] packet) : base(packet)
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
                   obj is AbstractArtPacketNetCommand<T> other &&
                   Command.Equals(other.Command);
        }
    }
}