using System;

namespace ArtNetSharp
{
    public readonly struct GoodInput : IEquatable<GoodInput>
    {
        public readonly byte Byte1;

        public readonly EConvertTo ConvertTo;
        public readonly bool ReceiveErrorsDetected;
        public readonly bool InputDisabled;
        public readonly bool DMX_TestPacketsSupported;
        public readonly bool DMX_SIPsSupported;
        public readonly bool DMX_TestPacketsSupported2;
        public readonly bool DataReceived;

        public GoodInput(in byte byte1)
        {
            Byte1 = byte1;

            ConvertTo = (EConvertTo)(Byte1 & 0b00000001);
            ReceiveErrorsDetected = Tools.BitsMatch(Byte1, 0b00000100);
            InputDisabled = Tools.BitsMatch(Byte1, 0b00001000);
            DMX_TestPacketsSupported = Tools.BitsMatch(Byte1, 0b00010000);
            DMX_SIPsSupported = Tools.BitsMatch(Byte1, 0b00100000);
            DMX_TestPacketsSupported2 = Tools.BitsMatch(Byte1, 0b01000000);
            DataReceived = Tools.BitsMatch(Byte1, 0b10000000);
        }

        public GoodInput(in EConvertTo convertTo = EConvertTo.ArtNet,
                         in bool receiveErrorsDetected = false,
                         in bool inputDisabled = false,
                         in bool dMX_TestPacketsSupported = false,
                         in bool dMX_SIPsSupported = false,
                         in bool dMX_TestPacketsSupported2 = false,
                         in bool dataReceived = false) : this()
        {
            ConvertTo = convertTo;
            ReceiveErrorsDetected = receiveErrorsDetected;
            InputDisabled = inputDisabled;
            DMX_TestPacketsSupported = dMX_TestPacketsSupported;
            DMX_SIPsSupported = dMX_SIPsSupported;
            DMX_TestPacketsSupported2 = dMX_TestPacketsSupported2;
            DataReceived = dataReceived;

            Byte1 |= (byte)ConvertTo;

            // Calculate Byte 1
            if (ReceiveErrorsDetected)
                Byte1 |= 0b00000100;
            if (InputDisabled)
                Byte1 |= 0b00001000;
            if (DMX_TestPacketsSupported)
                Byte1 |= 0b00010000;
            if (DMX_SIPsSupported)
                Byte1 |= 0b00100000;
            if (DMX_TestPacketsSupported2)
                Byte1 |= 0b01000000;
            if (DataReceived)
                Byte1 |= 0b10000000;
        }

        public static GoodInput operator |(in GoodInput goodInputA, in GoodInput goodInputB)
        {
            byte byte1 = (byte)(goodInputA.Byte1 | goodInputB.Byte1);
            return new GoodInput(byte1);
        }
        public static GoodInput operator &(in GoodInput goodInputA, in GoodInput goodInputB)
        {
            byte byte1 = (byte)(goodInputA.Byte1 & goodInputB.Byte1);
            return new GoodInput(byte1);
        }
        public static GoodInput operator ~(in GoodInput goodInput)
        {
            byte byte1 = (byte)~goodInput.Byte1;
            return new GoodInput(byte1);
        }

        public static bool operator ==(in GoodInput left, in GoodInput right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in GoodInput left, in GoodInput right)
        {
            return !(left == right);
        }
        public enum EConvertTo : byte
        {
            ArtNet = 0b00000000,
            sACN = 0b00000001,
        }
        public static implicit operator byte(in GoodInput goodInput)
        {
            return goodInput.Byte1;
        }
        public static implicit operator GoodInput(in byte b)
        {
            return new GoodInput(b);
        }


        public override bool Equals(object obj)
        {
            return obj is GoodInput status && Equals(status);
        }

        public bool Equals(GoodInput other)
        {
            return Byte1 == other.Byte1;
        }

        public override int GetHashCode()
        {
            int hashCode = -971048872;
            hashCode = hashCode * -1521134295 + Byte1.GetHashCode();
            return hashCode;
        }
    }
}