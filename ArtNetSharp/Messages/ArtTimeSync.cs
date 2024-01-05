using System;

namespace ArtNetSharp
{
    public sealed class ArtTimeSync : AbstractArtPacket
    {
        public override sealed EOpCodes OpCode => EOpCodes.OpTimeSync;
        protected override sealed ushort PacketMinLength => 24;

        public readonly bool Programming;
        public readonly EDaylightSaving DaylightSaving;
        public readonly DateTime DateTime;
        public ArtTimeSync(in bool programming,
            in DateTime dateTime,
            in EDaylightSaving daylightSaving = EDaylightSaving.Active,
            in ushort protocolVersion = Constants.PROTOCOL_VERSION)
            : base(protocolVersion)
        {
            Programming = programming;
            DateTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second);
            DaylightSaving = daylightSaving;
        }
        public ArtTimeSync(in byte[] packet) : base(packet)
        {
            Programming = packet[14] == 0xaa ? true : false;
            byte secounds = packet[15];
            byte minutes = packet[16];
            byte hours = packet[17];
            byte dayOfMonth = packet[18];
            byte month = packet[19];
            ushort yearFrom1900 = (ushort)(packet[20] << 8 | packet[21]);
            DayOfWeek dayOfWeek = (DayOfWeek)packet[22];
            DaylightSaving = (EDaylightSaving)packet[23];
            DateTime = new DateTime(yearFrom1900 + 1900, month, dayOfMonth, hours, minutes, secounds);
        }

        protected sealed override void fillPacket(ref byte[] p)
        {
            //p[12] = 0 // Filler 1
            //p[13] = 0 // Filler 2
            p[14] = (byte)(Programming ? 0xaa : 0); // Prog
            p[15] = (byte)DateTime.Second; // Secounds
            p[16] = (byte)DateTime.Minute; // Minutes
            p[17] = (byte)DateTime.Hour; // Hours
            p[18] = (byte)DateTime.Day; // DayOfMonth
            p[19] = (byte)DateTime.Month; // Month
            Tools.FromUShort((ushort)(DateTime.Year - 1900), out p[21], out p[20]);
            p[22] = (byte)DateTime.DayOfWeek; // DayOfWeek
            p[23] = (byte)DaylightSaving; // DaylightSaving
        }

        public static implicit operator byte[](ArtTimeSync artTimeCode)
        {
            return artTimeCode.GetPacket();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtTimeSync other
                && Programming == other.Programming
                && DateTime == other.DateTime
                && DaylightSaving == other.DaylightSaving;
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = hashCode * -1521134295 + Programming.GetHashCode();
            hashCode = hashCode * -1521134295 + DateTime.GetHashCode();
            hashCode = hashCode * -1521134295 + DaylightSaving.GetHashCode();
            return hashCode;
        }

        public override string ToString()
        {
            return $"{nameof(ArtTimeSync)}: Time: {DateTime}, Programming: {Programming}, DaylightSaving: {DaylightSaving}";
        }
    }
}