using System;

namespace ArtNetSharp
{
    public readonly struct ArtAddressCommand : IEquatable<ArtAddressCommand>
    {
        public static readonly ArtAddressCommand Default = new ArtAddressCommand();
        public readonly EArtAddressCommand Command;
        public readonly byte? Port;

        public ArtAddressCommand(in EArtAddressCommand command, in byte? port = null)
        {
            if (port == null)
                if ((((byte)command) & 0x0f) != ((byte)command))
                    throw new ArgumentException($"The given Command requires a port");

            if (port.HasValue && port.Value >= 4)
                throw new ArgumentOutOfRangeException($"{nameof(port)} has to be between 0 and 3");

            Command = command;
            Port = port;
        }
        public ArtAddressCommand(in byte data) : this(getCommand(data), getPort(data))
        {
        }

        private static EArtAddressCommand getCommand(in byte data)
        {
            if (data > 0x0f)
                return (EArtAddressCommand)(data & 0xf0);

            return (EArtAddressCommand)data;
        }

        private static byte? getPort(in byte data)
        {
            if (data < 0x0f)
                return null;

            return (byte)(data & 0x0f);
        }
        public static implicit operator byte(ArtAddressCommand command)
        {
            if (command.Port.HasValue)
                return (byte)((byte)command.Command | command.Port.Value);

            return (byte)command.Command;
        }
        public static implicit operator ArtAddressCommand(byte b)
        {
            return new ArtAddressCommand(b);
        }

        public override bool Equals(object obj)
        {
            return obj is ArtAddressCommand other &&
                   this.Equals(other);
        }

        public override int GetHashCode()
        {
            int hashCode = -971048872;
            hashCode = hashCode * -1521134295 + Command.GetHashCode();
            hashCode = hashCode * -1521134295 + Port.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(ArtAddressCommand a, ArtAddressCommand b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(ArtAddressCommand a, ArtAddressCommand b)
        {
            return !a.Equals(b);
        }

        public bool Equals(ArtAddressCommand other)
        {
            return Command == other.Command && Port == other.Port;
        }
    }
}