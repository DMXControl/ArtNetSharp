using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ArtNetSharp
{
    public sealed class ArtDataReply : AbstractArtPacket
    {
        public override sealed EOpCodes OpCode => EOpCodes.OpDataReply;
        protected override sealed ushort PacketMinLength => 42;
        protected override sealed ushort PacketMaxLength => (ushort)(PacketMinLength+512);
        protected override sealed ushort PacketBuildLength => (ushort)(PacketMinLength + (Data?.Length ?? 0));

        public readonly EDataRequest Request;
        public readonly ushort OemCode;
        /// <summary>
        /// The ESTA manufacturer code. The ESTA
        /// Manufacturer Code is assigned by ESTA and
        /// uniquely identifies the manufacturer.
        /// </summary>
        public readonly ushort ManufacturerCode;

        public readonly byte[] Data;

        public readonly object PayloadObject = null;

        private ArtDataReply()
        {

        }
        public ArtDataReply(in ushort oemCode = Constants.DEFAULT_OEM_CODE,
                       in ushort manufacturerCode = Constants.DEFAULT_ESTA_MANUFACTURER_CODE,
                       in EDataRequest request = EDataRequest.Poll,
                       in string payload = null,
                       in ushort protocolVersion = Constants.PROTOCOL_VERSION) : this(oemCode,manufacturerCode,request, !string.IsNullOrWhiteSpace(payload)? Encoding.ASCII.GetBytes(payload):null,protocolVersion)
        {
            PayloadObject = payload;
        }
        public ArtDataReply(in ushort oemCode = Constants.DEFAULT_OEM_CODE,
                       in ushort manufacturerCode = Constants.DEFAULT_ESTA_MANUFACTURER_CODE,
                       in EDataRequest request = EDataRequest.Poll,
                       in byte[] data = null,
                       in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(protocolVersion)
        {
            OemCode = oemCode;
            ManufacturerCode = manufacturerCode;
            Request = request;
            Data = data ?? new byte[0];
        }

        public ArtDataReply(in byte[] packet) : base(packet)
        {
            if (packet.Length >= 13)

            ManufacturerCode = (ushort)(packet[12] << 8 | packet[13]);
            OemCode = (ushort)(packet[14] << 8 | packet[15]);
            Request = (EDataRequest)(ushort)(packet[16] << 8 | packet[17]);
            ushort payloadLength= (ushort)(packet[18] << 8 | packet[19]);
            Data = new byte[payloadLength];
            Array.Copy(packet, 20, Data, 0, Data.Length);
            if((ushort)Request <=8) // Data is String/URL
                PayloadObject = Encoding.ASCII.GetString(Data, 0, Data.Length).TrimEnd('\0');
        }

        protected sealed override void fillPacket(ref byte[] p)
        {
            Tools.FromUShort(ManufacturerCode, out p[13], out p[12]);// manufacturer code
            Tools.FromUShort(OemCode, out p[15], out p[14]);// OEM code
            Tools.FromUShort((ushort)Request, out p[17], out p[16]);// Request
            Tools.FromUShort((ushort)Data.Length, out p[19], out p[18]);// PayloadLength
            Array.Copy(Data, 0, p, 20, Data.Length);

        }
        public static implicit operator byte[](ArtDataReply artDataReply)
        {
            return artDataReply.GetPacket();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtDataReply other
                && ManufacturerCode == other.ManufacturerCode
                && OemCode == other.OemCode
                && Request == other.Request
                && Data.SequenceEqual(other.Data);
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = hashCode * -1521134295 + ManufacturerCode.GetHashCode();
            hashCode = hashCode * -1521134295 + OemCode.GetHashCode();
            hashCode = hashCode * -1521134295 + Request.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<byte[]>.Default.GetHashCode(Data);
            return hashCode;
        }

        public override string ToString()
        {
            return $"{nameof(ArtDataReply)}: Request:{Request}, OEM:{OemCode:x4}, Manuf.:{ManufacturerCode:x4} PayloadObject: {PayloadObject}";
        }
    }
}