using System.Linq;

namespace ArtNetSharp
{
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public sealed class ArtIpProgReply : AbstractArtPacket
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    {
        public override sealed EOpCodes OpCode => EOpCodes.OpIpProgReply;
        protected override sealed ushort PacketMinLength => 34;

        public readonly IPv4Address Ip;
        public readonly IPv4Address SubnetMask;
        public readonly IPv4Address DefaultGateway;
        public readonly ushort Port;
        public readonly EArtIpProgReplyStatusFlags Status;


        public ArtIpProgReply(in IPv4Address ip,
                        in IPv4Address subnetMask,
                        in IPv4Address defaultGateway,
                        in EArtIpProgReplyStatusFlags status,
                        in ushort port = Constants.ARTNET_PORT,
                        in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(protocolVersion)
        {
            Ip = ip;
            SubnetMask = subnetMask;
            DefaultGateway = defaultGateway;
            Port = port;
            Status = status;
        }
        public ArtIpProgReply(in byte[] packet) : base(packet)
        {
            Status = (EArtIpProgReplyStatusFlags)packet[26];

            Port = (ushort)(packet[25] << 8 | packet[24]);
            Ip = new IPv4Address(packet.Skip(16).Take(4));
            SubnetMask = new IPv4Address(packet.Skip(20).Take(4));
            DefaultGateway = new IPv4Address(packet.Skip(28).Take(4));
        }

        protected sealed override void fillPacket(ref byte[] p)
        {
            //p[12] = 0; // Filler 1
            //p[13] = 0; // Filler 2
            //p[14] = 0; // Filler 3
            //p[15] = 0; // Filler 4
            p[16] = Ip.B1; // IP Address 1
            p[17] = Ip.B2; // IP Address 2
            p[18] = Ip.B3; // IP Address 3
            p[19] = Ip.B4; // IP Address 4
            p[20] = SubnetMask.B1; // SubnetMask 1
            p[21] = SubnetMask.B2; // SubnetMask 2
            p[22] = SubnetMask.B3; // SubnetMask 3
            p[23] = SubnetMask.B4; // SubnetMask 4
            Tools.FromUShort(Constants.ARTNET_PORT, out p[24], out p[25]); // Port
            p[26] = (byte)Status;
            //p[27] = 0; // Spare 2
            p[28] = DefaultGateway.B1; // DefaultGateway 1
            p[29] = DefaultGateway.B2; // DefaultGateway 2
            p[30] = DefaultGateway.B3; // DefaultGateway 3
            p[31] = DefaultGateway.B4; // DefaultGateway 4
            //p[32] = 0; // Spare 7
            //p[33] = 0; // Spare 8
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtIpProgReply other
                && Ip.Equals(other.Ip)
                && SubnetMask.Equals(other.SubnetMask)
                && DefaultGateway.Equals(other.DefaultGateway)
                && Port == other.Port
                && Status == other.Status;
        }
    }
}