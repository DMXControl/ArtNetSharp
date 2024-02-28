using RDMSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ArtNetSharp
{
    public sealed class ArtTodData : AbstractArtPacketNetAddressCommand<EArtTodDataCommandResponse>
    {
        public override sealed EOpCodes OpCode => EOpCodes.OpTodData;
        protected override sealed ushort PacketMinLength => 28;
        protected override sealed ushort PacketMaxLength => (ushort)(PacketMinLength + (MaxUidsPerPacket * 8));
        protected override sealed ushort PacketBuildLength => (ushort)(PacketMinLength + ((Uids?.Length ?? 0) * 8));
        protected override sealed ushort NetByte => 21;
        protected override sealed ushort CommandByte => 22;
        protected override sealed ushort AddressByte => 23;

        public const byte MaxUidsPerPacket = 200;

        public readonly RDMUID[] Uids;
        public readonly ERDMVersion RdmVersion;
        /// <summary>
        /// Physical port index. Range 1-4. This number is used in combination with BindIndex to identify the physical port that generated the packet.
        /// This is done by referencing data in the ArtPollReply with a matching BindIndex:
        /// ArtPollReplyData -> BindIndex == ArtTodData - > BindIndex
        /// An ArtPollReply can encode between 1 and 4 physical ports, defined by ArtPollReply - > NumPortsLo.
        /// This number must be used when calculating the physical port in order to allow for the variable encoding.
        /// The calculation is:
        /// Physical Port = (BindIndex - 1) * ArtPollReply - > NumPortsLo + ArtTodData->Port
        /// As most modern Art-Net gateways implement one universe per ArtPollReply, ArtTodData -> Port will usually be set to a value of 1.
        /// </summary>
        public readonly byte Port;
        /// <summary>
        /// The BindIndexdefines the bound node which
        /// originated this packet.In combination with Port and
        /// Source IP address, it uniquely identifiesthe sender.
        /// This must match the BindIndex field in ArtPollReply.
        /// This number represents the order of bound devices.
        /// A lower number means closer to root device. A value
        /// of 1 means root device.
        /// </summary>
        public readonly byte BindIndex;
        /// <summary>
        /// The total number of RDM devices discovered by this
        /// Universe.
        /// </summary>
        public readonly ushort UidTotalCount;
        /// <summary>
        /// The index number of this packet. When UidTotal
        /// exceeds 200, multiple ArtTodData packets are used.
        /// BlockCount is set to zero for the first packet and
        /// incremented for each subsequent packet containing
        /// blocks of TOD information.
        /// </summary>
        public readonly byte BlockCount;

        public ArtTodData(in PortAddress portAddress,
                      in byte port,
                      in byte bindIndex,
                      in ushort uidTotalCount,
                      in byte blockCount,
                      in RDMUID[] uids = default,
                      in EArtTodDataCommandResponse command = EArtTodDataCommandResponse.TodNak,
                      in ERDMVersion rdmVersion = ERDMVersion.STANDARD_V1_0,
                      in ushort protocolVersion = Constants.PROTOCOL_VERSION) : this(portAddress.Net, portAddress.Address, port, bindIndex, uidTotalCount, blockCount, uids, command, rdmVersion, protocolVersion)
        {
        }

        public ArtTodData(in Net net,
                  in Address address,
                  in byte port,
                  in byte bindIndex,
                  in ushort uidTotalCount,
                  in byte blockCount,
                  in RDMUID[] uids = default,
                  in EArtTodDataCommandResponse command = EArtTodDataCommandResponse.TodNak,
                  in ERDMVersion rdmVersion = ERDMVersion.STANDARD_V1_0,
                  in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(net, address, command, protocolVersion)
        {
            if (uids.Length > MaxUidsPerPacket)
                throw new ArgumentOutOfRangeException($"The limit of UIDs per Package is {MaxUidsPerPacket}");

            Port = port;
            BindIndex = bindIndex;
            UidTotalCount = uidTotalCount;
            BlockCount = blockCount;

            Uids = uids;
            RdmVersion = rdmVersion;
        }
        public ArtTodData(in byte[] packet) : base(packet)
        {
            RdmVersion = (ERDMVersion)packet[12];
            Port = packet[13];
            BindIndex = packet[20];

            UidTotalCount = (ushort)((packet[24] << 8) | packet[25]);
            BlockCount = packet[26];
            byte count = packet[27];
            List<RDMUID> uids = new List<RDMUID>();
            byte[] buffer = new byte[8];
            for (int i = 0; i < count; i++)
            {
                int index = 28 + (i * 6);
                for (int j = 0; j < 6; j++)
                    buffer[5 - j] = packet[index + j];
                RDMUID uid = new RDMUID(BitConverter.ToUInt64(buffer, 0));
                uids.Add(uid);
            }
            this.Uids = uids.ToArray();
        }
        protected sealed override void fillPacket(ref byte[] p)
        {
            base.fillPacket(ref p);

            p[12] = (byte)RdmVersion; // RdmVersion
            p[13] = Port; // Port
            //p[14] = 0; // Spare 1
            //p[15] = 0; // Spare 2
            //p[16] = 0; // Spare 3
            //p[17] = 0; // Spare 4
            //p[18] = 0; // Spare 5
            //p[19] = 0; // Spare 6
            p[20] = BindIndex; // BindIndex
            //p[21] = 0; // Net (done by Abstract part)
            //p[22] = 0; // Command (done by Abstract part)
            //p[23] = 0; // Address (done by Abstract part)
            Tools.FromUShort(UidTotalCount, out p[25], out p[24]); //UidTotalCount
            p[26] = BlockCount; // BlockCount
            p[27] = (byte)Uids.Length; // UidCount

            List<byte> data = new List<byte>();
            foreach (RDMUID uid in Uids)
                data.AddRange(uid.ToBytes());

            Array.Copy(data.ToArray(), 0, p, 28, Uids.Length * 6);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtTodData data
                && Uids.SequenceEqual(data.Uids)
                && RdmVersion == data.RdmVersion
                && Port == data.Port
                && BindIndex == data.BindIndex
                && UidTotalCount == data.UidTotalCount
                && BlockCount == data.BlockCount;
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<RDMUID[]>.Default.GetHashCode(Uids);
            hashCode = hashCode * -1521134295 + RdmVersion.GetHashCode();
            hashCode = hashCode * -1521134295 + Port.GetHashCode();
            hashCode = hashCode * -1521134295 + BindIndex.GetHashCode();
            hashCode = hashCode * -1521134295 + UidTotalCount.GetHashCode();
            hashCode = hashCode * -1521134295 + BlockCount.GetHashCode();
            return hashCode;
        }

        public static implicit operator byte[](ArtTodData artTodData)
        {
            return artTodData.GetPacket();
        }
        public override string ToString()
        {
            string uids = string.Empty;
            if (Uids.Length != 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (RDMUID uid in Uids)
                    sb.Append($"{uid}, ");
                uids = sb.ToString().Trim().TrimEnd(',');
            }
            return $"{nameof(ArtTodData)}: Port: {Port} - #{BindIndex} UIDs[{Uids.Length}]: {uids}";
        }
    }
}