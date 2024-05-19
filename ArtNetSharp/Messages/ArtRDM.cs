using RDMSharp;
using System;
using System.Linq;

namespace ArtNetSharp
{
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public sealed class ArtRDM : AbstractArtPacketNetAddressCommand<EArtRDMCommand>
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    {
        public override sealed EOpCodes OpCode => EOpCodes.OpRdm;
        protected override sealed ushort PacketMinLength => 24;
        protected override sealed ushort PacketMaxLength => ushort.MaxValue;
        protected override sealed ushort PacketBuildLength => (ushort)(PacketMinLength + (Data.Length - 1));
        protected override sealed ushort NetByte => 21;
        protected override sealed ushort CommandByte => 22;
        protected override sealed ushort AddressByte => 23;

        public RDMUID Source => RDMMessage.SourceUID;
        public RDMUID Destination => RDMMessage.DestUID;
        public byte Transaction => RDMMessage.TransactionCounter;

        public readonly byte[] Data;
        public readonly RDMMessage RDMMessage;
        public readonly ERDMVersion RdmVersion;
        public ArtRDM(in PortAddress portAddress,
                      in RDMMessage rdmMessage,
                      in EArtRDMCommand command = EArtRDMCommand.ArProcess,
                      in ERDMVersion rdmVersion = ERDMVersion.STANDARD_V1_0,
                      in ushort protocolVersion = Constants.PROTOCOL_VERSION) : this(portAddress.Net, portAddress.Address, rdmMessage, command, rdmVersion, protocolVersion)
        {
        }
        public ArtRDM(in Net net,
                  in Address address,
                  in RDMMessage rdmMessage,
                  in EArtRDMCommand command = EArtRDMCommand.ArProcess,
                  in ERDMVersion rdmVersion = ERDMVersion.STANDARD_V1_0,
                  in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(net, address, command, protocolVersion)
        {
            if (rdmMessage == null)
                throw new ArgumentNullException(nameof(rdmMessage));

            RDMMessage = rdmMessage;
            RdmVersion = rdmVersion;
            Data = RDMMessage.BuildMessage();
        }
        public ArtRDM(in byte[] packet) : base(packet)
        {
            RdmVersion = (ERDMVersion)packet[12];

            Data = new byte[(packet.Length - 24) + 1];
            Data[0] = 0xcc;
            Array.Copy(packet, 24, Data, 1, Data.Length - 1);

            RDMMessage = new RDMMessage(Data);
        }
        protected sealed override void fillPacket(ref byte[] p)
        {
            base.fillPacket(ref p);

            p[12] = (byte)RdmVersion;
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
            Array.Copy(Data, 1, p, 24, Data.Length - 1);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtRDM other
                && RdmVersion == other.RdmVersion
                && Data.SequenceEqual(other.Data);
        }

        public override string ToString()
        {
            return $"{nameof(ArtRDM)}: {PortAddress.Combined:x4} Command: {RDMMessage.Command}, Parameter: {RDMMessage.Parameter}, ResponseType: {RDMMessage.ResponseType}, Transaction: {Transaction}, Source: {Source}, Destination: {Destination}";
        }
    }
}