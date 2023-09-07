namespace ArtNetSharp
{
    public abstract class AbstractArtPacketNetCommand<T> : AbstractArtPacketNet
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

        public static implicit operator byte[](AbstractArtPacketNetCommand<T> abstractArtPacketNetCommand)
        {
            return abstractArtPacketNetCommand.GetPacket();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj) &&
                   obj is AbstractArtPacketNetCommand<T> other &&
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