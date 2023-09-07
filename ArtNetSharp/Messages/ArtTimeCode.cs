using System;

namespace ArtNetSharp
{
    public sealed class ArtTimeCode : AbstractArtPacket
    {
        public override sealed EOpCodes OpCode => EOpCodes.OpTimeCode;
        protected override sealed ushort PacketMinLength => 19;

        public readonly byte Frames;
        public readonly byte Secounds;
        public readonly byte Minutes;
        public readonly byte Hours;
        public readonly ETimecodeType Type;

        public ArtTimeCode(in byte frames,
                           in byte secounds,
                           in byte minutes,
                           in byte hours,
                           in ETimecodeType type,
                           in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(protocolVersion)
        {
            if (frames > 29)
                throw new ArgumentOutOfRangeException($"{nameof(frames)} has to be between 0 and 29");
            if (secounds > 59)
                throw new ArgumentOutOfRangeException($"{nameof(secounds)} has to be between 0 and 59");
            if (minutes > 59)
                throw new ArgumentOutOfRangeException($"{nameof(minutes)} has to be between 0 and 59");
            if (hours > 23)
                throw new ArgumentOutOfRangeException($"{nameof(hours)} has to be between 0 and 23");

            Frames = frames;
            Secounds = secounds;
            Minutes = minutes;
            Hours = hours;
            Type = type;
        }
        public ArtTimeCode(in byte[] packet) : base(packet)
        {
            Frames = packet[14];
            Secounds = packet[15];
            Minutes = packet[16];
            Hours = packet[17];
            Type = (ETimecodeType)packet[18];
        }

        protected sealed override void fillPacket(ref byte[] p)
        {
            //p[12] = 0 // Filler 1
            //p[13] = 0 // Filler 2
            p[14] = Frames; // Frames
            p[15] = Secounds; // Secounds
            p[16] = Minutes; // Minutes
            p[17] = Hours; // Hours
            p[18] = (byte)Type; // Type
        }

        public static implicit operator byte[](ArtTimeCode artTimeCode)
        {
            return artTimeCode.GetPacket();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtTimeCode other
                && Frames == other.Frames
                && Secounds == other.Secounds
                && Minutes == other.Minutes
                && Hours == other.Hours
                && Type == other.Type;
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = hashCode * -1521134295 + Frames.GetHashCode();
            hashCode = hashCode * -1521134295 + Secounds.GetHashCode();
            hashCode = hashCode * -1521134295 + Minutes.GetHashCode();
            hashCode = hashCode * -1521134295 + Hours.GetHashCode();
            hashCode = hashCode * -1521134295 + Type.GetHashCode();
            return hashCode;
        }

        public override string ToString()
        {
            return $"{nameof(ArtTimeCode)}: Type: {Type}, Time: {new TimeSpan(Hours, Minutes, Secounds)}, Frames: {Frames}";
        }
    }
}