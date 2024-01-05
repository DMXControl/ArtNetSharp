using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtNetSharp
{
    public sealed class ArtVlc : AbstractArtPacketNetAddress
    {
        /// <summary>
        /// The sequence number is used to ensure that ArtDmx packets are used in the correct order.
        /// When Art-Net is carried over a medium such as the Internet, it is possible that ArtDmx packets will reach the receiver out of order.
        /// This field is incremented in the range 0x01 to 0xff to allow the receiving node to re-sequence packets.
        /// The Sequence field is set to 0x00 to disable this feature.
        /// </summary>
        public readonly byte Sequence;

        public readonly EVlcFlags Flags;
        /// <summary>
        /// The transaction number is a 16-bit value which allows VLC transactions to be synchronised.
        /// A value of 0 indicates the first packet in a transaction.
        /// A value of ffff16 indicates the final packet in the transaction.
        /// All other packets contain consecutive numbers which increment on each packet and roll over to 1 at fffe.
        /// </summary>
        public readonly ushort Transaction;
        /// <summary>
        /// The slot number, range 1-512, of the device to which this packet is directed.
        /// A value 0f 0 indicates that all devices attached to this packet’s Port-Address should accept the packet.
        /// </summary>
        public readonly ushort SlotAddress;
        /// <summary>
        /// The 16-bit unsigned additive checksum of the data in the payload.
        /// </summary>
        public readonly ushort PayloadChecksum;
        /// <summary>
        /// The 8-bit VLC modulation depth expressed as a percentage in the range 1 to 100.
        /// A value of 0 indicates that the transmitter should use its default value.
        /// </summary>
        public readonly byte Depth;
        /// <summary>
        /// The 16-bit modulation frequency of the VLC transmitter expressed in Hz.
        /// A value of 0 indicates that the transmitter should use its default value.
        /// </summary>
        public readonly ushort ModulationFrequency;
        /// <summary>
        /// The 16-bit modulation type number that the transmitter should use to transmit VLC. 0000 – Use transmitter default
        /// </summary>
        public readonly ushort ModulationType;
        /// <summary>
        /// The 16-bit payload language code.
        /// </summary>
        public readonly EPayloadLanguageCode PayloadLanguageCode;
        public readonly ushort BeaconModeRepeatFrequency;
        public readonly byte[] Payload;

        private const ushort MaxPayloadLength = 480;

        public override sealed EOpCodes OpCode => EOpCodes.OpNzs;
        protected override sealed ushort PacketMinLength => 40;
        protected override sealed ushort PacketMaxLength => 40 + MaxPayloadLength;
        protected override sealed ushort PacketBuildLength => (ushort)(40 + (Payload?.Length ?? 0));

        protected override ushort NetByte => 15;
        protected override ushort AddressByte => 14;

        public ArtVlc(in byte sequence,
                      in Net net,
                      in Address address,
                      in byte[] payload,
                      in ushort transaction,
                      in ushort payloadChecksum,
                      in ushort slotAddress = 0,
                      in byte depth = 0,
                      in ushort modulationFrequency = 0,
                      in ushort modulationType = 0,
                      in ushort beaconModeRepeatFrequency = 0,
                      in EVlcFlags flags = EVlcFlags.None,
                      in EPayloadLanguageCode payloadLanguageCode = EPayloadLanguageCode.BeaconURL,
                      in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(net, address, protocolVersion)
        {
            if (payload.Length > MaxPayloadLength)
                throw new ArgumentOutOfRangeException($"Payload max length is {MaxPayloadLength}");

            Sequence = sequence;
            Flags = flags;
            Transaction = transaction;
            SlotAddress = slotAddress;
            PayloadChecksum = payloadChecksum;
            Depth = depth;
            ModulationFrequency = modulationFrequency;
            ModulationType = modulationType;
            PayloadLanguageCode = payloadLanguageCode;
            BeaconModeRepeatFrequency = beaconModeRepeatFrequency;
            Payload = payload;
        }
        public ArtVlc(in byte[] packet) : base(packet)
        {
            if (!IsArtVlc(packet))
                throw new ArgumentException("This given data isn't an ArtVlcPacket!");

            Sequence = packet[12]; // 5 Sequence

            Flags = (EVlcFlags)packet[21]; // 14 Flags
            Transaction = (ushort)((packet[22] << 8) | packet[23]); // 15 & 16 TransHi & TransLo
            SlotAddress = (ushort)((packet[24] << 8) | packet[25]); // 17 & 18 SlotAddrHi & SlotAddrLo
            ushort vlcPayloadLength = (ushort)((packet[26] << 8) | packet[27]); // 19 & 20 PayCountHi & PayCountLo
            PayloadChecksum = (ushort)((packet[28] << 8) | packet[29]); // 21 & 22 PayCheckHi & PayCheckLo
            // 23 Spare 1
            Depth = packet[31]; // 24 VlcDepth
            ModulationFrequency = (ushort)((packet[32] << 8) | packet[33]); // 25 & 26 VlcFreqHi & VlcFreqLo
            ModulationType = (ushort)((packet[34] << 8) | packet[35]); // 27 & 28 VlcModHi & VlcModLo
            PayloadLanguageCode = (EPayloadLanguageCode)(ushort)((packet[36] << 8) | packet[37]); // 29 & 30 PayLangHi & PayLangLo
            BeaconModeRepeatFrequency = (ushort)((packet[38] << 8) | packet[39]); // 31 & 32 BeacRepHi & BeacRepLo
            if (vlcPayloadLength <= MaxPayloadLength)
            {
                Payload = new byte[vlcPayloadLength];
                Array.Copy(packet, 40, Payload, 0, vlcPayloadLength);
            }
            else Payload = default;
        }

        protected sealed override void fillPacket(ref byte[] p)
        {
            base.fillPacket(ref p);
            p[12] = Sequence;

            p[13] = 0x91; // 6 StartCode The DMX512 start code of this packetis set to ox91. No other values are allowed.
            p[18] = 0x41; // 11 Vlc[0] ManIdHi 0x41 Magic number used to identify this packet
            p[19] = 0x4C; // 12 Vlc[1] ManIdLo 0x4C Magic number used to identify this packet
            p[20] = 0x45; // 13 Vlc[2] SubCode 0x45 Magic number used to identify this packet

            //p[14] = 0; // Address (done by Abstract part)
            //p[15] = 0; // Net (done by Abstract part)
            p[21] = (byte)Flags; // 14 Flags
            p[22] = (byte)((Transaction >> 8) & 0xff); // 15 TransHi
            p[23] = (byte)(Transaction & 0xff);        // 16 TransLo
            p[24] = (byte)((SlotAddress >> 8) & 0xff); // 17 SlotAddrHi
            p[25] = (byte)(SlotAddress & 0xff);        // 18 SlotAddrLo
            p[26] = (byte)((Payload.Length >> 8) & 0xff); // 19 PayCountHi
            p[27] = (byte)(Payload.Length & 0xff);        // 20 PayCountLo
            p[28] = (byte)((PayloadChecksum >> 8) & 0xff); // 21 PayCheckHi
            p[29] = (byte)(PayloadChecksum & 0xff);        // 22 PayCheckLo
            //p[30] = 0; // 23 Spare 1
            p[31] = Depth; // 24 VlcDepth
            p[32] = (byte)((ModulationFrequency >> 8) & 0xff); // 25 VlcFreqHi
            p[33] = (byte)(ModulationFrequency & 0xff);        // 26 VlcFreqLo
            p[34] = (byte)((ModulationType >> 8) & 0xff); // 27 VlcModHi
            p[35] = (byte)(ModulationType & 0xff);        // 28 VlcModLo
            p[36] = (byte)(((byte)PayloadLanguageCode >> 8) & 0xff); // 29 PayLangHi
            p[37] = (byte)((byte)PayloadLanguageCode & 0xff);        // 30 PayLangLo
            p[38] = (byte)((BeaconModeRepeatFrequency >> 8) & 0xff); // 31 BeacRepHi
            p[39] = (byte)(BeaconModeRepeatFrequency & 0xff);        // 32 BeacRepLo
            Array.Copy(Payload, 0, p, 40, Payload.Length);
        }
        public static implicit operator byte[](ArtVlc artVlc)
        {
            return artVlc.GetPacket();
        }
        public override bool Equals(object obj)
        {

            return base.Equals(obj)
                && obj is ArtVlc other
                && this.Sequence == other.Sequence
                && this.Flags == other.Flags
                && this.Transaction == other.Transaction
                && this.SlotAddress == other.SlotAddress
                && this.PayloadChecksum == other.PayloadChecksum
                && this.Depth == other.Depth
                && this.ModulationFrequency == other.ModulationFrequency
                && this.ModulationType == other.ModulationType
                && this.PayloadLanguageCode == other.PayloadLanguageCode
                && this.BeaconModeRepeatFrequency == other.BeaconModeRepeatFrequency
                && this.Payload.SequenceEqual(other.Payload);
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = hashCode * -1521134295 + Sequence.GetHashCode();
            hashCode = hashCode * -1521134295 + Flags.GetHashCode();
            hashCode = hashCode * -1521134295 + Transaction.GetHashCode();
            hashCode = hashCode * -1521134295 + SlotAddress.GetHashCode();
            hashCode = hashCode * -1521134295 + PayloadChecksum.GetHashCode();
            hashCode = hashCode * -1521134295 + Depth.GetHashCode();
            hashCode = hashCode * -1521134295 + ModulationFrequency.GetHashCode();
            hashCode = hashCode * -1521134295 + ModulationType.GetHashCode();
            hashCode = hashCode * -1521134295 + PayloadLanguageCode.GetHashCode();
            hashCode = hashCode * -1521134295 + BeaconModeRepeatFrequency.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<byte[]>.Default.GetHashCode(Payload);
            return hashCode;
        }

        public static bool IsArtVlc(byte[] p)
        {
            if (p[13] != 0x91)
                return false;

            if (p[18] != 0x41)
                return false;

            if (p[19] != 0x4C)
                return false;

            if (p[20] != 0x45)
                return false;

            return true;
        }
    }
}