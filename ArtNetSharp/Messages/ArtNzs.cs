using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtNetSharp
{
    public sealed class ArtNzs : AbstractArtPacketNetAddress
    {
        /// <summary>
        /// The sequence number is used to ensure that ArtDmx packets are used in the correct order.
        /// When Art-Net is carried over a medium such as the Internet, it is possible that ArtDmx packets will reach the receiver out of order.
        /// This field is incremented in the range 0x01 to 0xff to allow the receiving node to re-sequence packets.
        /// The Sequence field is set to 0x00 to disable this feature.
        /// </summary>
        public readonly byte Sequence;
        /// <summary>
        /// The DMX512 start code of this packet. Must not be Zero or RDM.
        /// </summary>
        public readonly byte StartCode;
        public readonly byte[] Data;

        public override sealed EOpCodes OpCode => EOpCodes.OpNzs;
        protected override sealed ushort PacketMinLength => 20;
        protected override sealed ushort PacketMaxLength => 18 + 512;
        protected override sealed ushort PacketBuildLength => (ushort)(18 + (Data?.Length ?? 0));

        protected override ushort NetByte => 15;
        protected override ushort AddressByte => 14;

        public ArtNzs(in byte sequence,
                      in byte startCode,
                      in Net net,
                      in Address address,
                      in byte[] data = default,
                      in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(net, address, protocolVersion)
        {
            if (startCode == Constants.DMX_STARTCODE)
                throw new ArgumentException($"Argument: {nameof(startCode)} is {startCode} (0x{startCode:x2}), this code is not allowed, its the DMX start code");
            if (startCode == Constants.RDM_STARTCODE)
                throw new ArgumentException($"Argument: {nameof(startCode)} is {startCode} (0x{startCode:x2}), this code is not allowed, its the RDM start code");
            Sequence = sequence;
            StartCode = startCode;
            Data = data;
        }
        public ArtNzs(in byte[] packet) : base(packet)
        {
            Sequence = packet[12];
            StartCode = packet[13];

            ushort length = (ushort)((packet[16] << 8) | packet[17]);
            if (length <= 512)
            {
                Data = new byte[length];
                Array.Copy(packet, 18, Data, 0, length);
            }
            else Data = default;
        }

        protected sealed override void fillPacket(ref byte[] p)
        {
            base.fillPacket(ref p);
            p[12] = Sequence;
            p[13] = StartCode;
            //p[14] = 0; // Address (done by Abstract part)
            //p[15] = 0; // Net (done by Abstract part)
            p[16] = (byte)((Data.Length >> 8) & 0xff); // LengthHi
            p[17] = (byte)(Data.Length & 0xff);        // LengthLo
            Array.Copy(Data, 0, p, 18, Data.Length);
        }
        public static implicit operator byte[](ArtNzs artNzs)
        {
            return artNzs.GetPacket();
        }
        public override bool Equals(object obj)
        {

            return base.Equals(obj)
                && obj is ArtNzs other
                && this.Sequence == other.Sequence
                && this.StartCode == other.StartCode
                && this.Data.SequenceEqual(other.Data);
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = hashCode * -1521134295 + Sequence.GetHashCode();
            hashCode = hashCode * -1521134295 + StartCode.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<byte[]>.Default.GetHashCode(Data);
            return hashCode;
        }
    }
}