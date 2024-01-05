using System;
using System.Linq;
using System.Text;

namespace ArtNetSharp
{
    public sealed class ArtTodRequest : AbstractArtPacketNetCommand<EArtTodRequestCommand>
    {
        public sealed override EOpCodes OpCode => EOpCodes.OpTodRequest;
        protected sealed override ushort PacketMinLength => 24;
        protected sealed override ushort PacketMaxLength => 56;
        protected sealed override ushort PacketBuildLength => 56;
        protected sealed override ushort NetByte => 21;
        protected sealed override ushort CommandByte => 22;

        public readonly Address[] Addresses;
        public readonly PortAddress[] PortAddresses;


        public ArtTodRequest(in PortAddress portAddress,
                             in EArtTodRequestCommand command = EArtTodRequestCommand.TodFull,
                             in ushort protocolVersion = Constants.PROTOCOL_VERSION)
            : this(portAddress.Net, new Address[] { portAddress.Address }, command, protocolVersion)
        {
        }
        public ArtTodRequest(in Net net,
                             in Address address,
                             in EArtTodRequestCommand command = EArtTodRequestCommand.TodFull,
                             in ushort protocolVersion = Constants.PROTOCOL_VERSION)
            : this(net, new Address[] { address }, command, protocolVersion)
        {
        }
        public ArtTodRequest(in byte net,
                         in Address[] address,
                         in EArtTodRequestCommand command = EArtTodRequestCommand.TodFull,
                         in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(net, command, protocolVersion)
        {
            Addresses = address.Take(32).ToArray();
            PortAddresses = Addresses.Select(a => new PortAddress(Net, a)).ToArray();
        }
        public ArtTodRequest(in byte[] packet) : base(packet)
        {
            Addresses = new Address[packet[23]];
            for (byte i = 0; i < packet[23]; i++)
                Addresses[i] = packet[24 + i];

            PortAddresses = Addresses.Select(a => new PortAddress(Net, a)).ToArray();
        }
        protected override void fillPacket(ref byte[] p)
        {
            base.fillPacket(ref p);

            //p[12] = 0; // Filler 1
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
            p[23] = (byte)Addresses.Length; // AddCount
            for (byte i = 0; i < p[23]; i++)
                p[24 + i] = Addresses[i];
        }

        public static implicit operator byte[](ArtTodRequest artTodRequest)
        {
            return artTodRequest.GetPacket();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                   && obj is ArtTodRequest other
                   && Addresses.SequenceEqual(other.Addresses);
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = hashCode * -1521134295 + Addresses.GetHashCode();
            return hashCode;
        }
        public override string ToString()
        {
            string portAddresses = string.Empty;
            if (PortAddresses.Length != 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var portAddress in PortAddresses)
                    sb.Append($"{portAddress.Combined:x4}, ");
                portAddresses = sb.ToString().Trim().TrimEnd(',');
            }
            return $"{nameof(ArtTodRequest)}:  Command: {Command}, PortAddresses[{PortAddresses.Length}]: {portAddresses}";
        }
    }
}