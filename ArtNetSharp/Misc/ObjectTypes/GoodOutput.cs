using System;

namespace ArtNetSharp
{
    public readonly struct GoodOutput : IEquatable<GoodOutput>
    {
        public static GoodOutput DATA_TRANSMITTED = new GoodOutput(isBeingOutputAsDMX: true);
        public readonly byte Byte1;
        public readonly byte Byte2;

        /// <summary>
        /// Input is selected to convert to sACN or Art-Net.
        /// </summary>
        public readonly EConvertFrom ConvertFrom;
        /// <summary>
        /// Merge Mode is HTP or LTP.
        /// </summary>
        public readonly EMergeMode MergeMode;
        /// <summary>
        /// DMX output short detected on power up
        /// </summary>
        public readonly bool DMX_OutputShortCircuit;
        /// <summary>
        /// Output is merging ArtNet data.
        /// </summary>
        public readonly bool MergingArtNetData;
        /// <summary>
        /// Channel includes DMX512 text packets. 
        /// </summary>
        public readonly bool DMX_TextPacketsSupported;
        /// <summary>
        /// Channel includes DMX512 SIP’s
        /// </summary>
        public readonly bool DMX_SIPsSupported;
        /// <summary>
        /// Channel includes DMX512 test packets. 
        /// </summary>
        public readonly bool DMX_TestPacketsSupported;
        /// <summary>
        /// ArtDmx or sACN data is being output as
        /// DMX512 on this port.
        /// </summary>
        public readonly bool IsBeingOutputAsDMX;

        /// <summary>
        /// 
        /// </summary>
        public readonly EOutputStyle OutputStyle;
        public readonly bool RDMisDisabled;
        public readonly bool DiscoveryIsCurrentlyRunning;
        public readonly bool BackgroundDiscoveryIsEnabled;

        public static GoodOutput None = new GoodOutput();

        public GoodOutput(in byte byte1, in byte byte2)
        {
            Byte1 = byte1;
            // Bit 0-3 Not used, set to zero (Mask: 0b11110000 -> ~0b00001111)
            Byte2 = (byte)(byte2 & 0b11110000);

            ConvertFrom = (EConvertFrom)(Byte1 & 0b00000001);
            MergeMode = (EMergeMode)(Byte1 & 0b00000010);
            DMX_OutputShortCircuit = Tools.BitsMatch(Byte1, 0b00000100);
            MergingArtNetData = Tools.BitsMatch(Byte1, 0b00001000);
            DMX_TextPacketsSupported = Tools.BitsMatch(Byte1, 0b00010000);
            DMX_SIPsSupported = Tools.BitsMatch(Byte1, 0b00100000);
            DMX_TestPacketsSupported = Tools.BitsMatch(Byte1, 0b01000000);
            IsBeingOutputAsDMX = Tools.BitsMatch(Byte1, 0b10000000);

            BackgroundDiscoveryIsEnabled = Tools.BitsMatch(Byte2, 0b00010000);
            DiscoveryIsCurrentlyRunning = Tools.BitsMatch(Byte2, 0b00100000);
            OutputStyle = (EOutputStyle)(Byte2 & 0b01000000);
            RDMisDisabled = Tools.BitsMatch(Byte2, 0b10000000);
        }

        public GoodOutput(in EConvertFrom convertFrom = EConvertFrom.ArtNet,
                         in EMergeMode mergeMode = EMergeMode.HTP,
                         in bool dmx_OutputShortCircuit = false,
                         in bool mergingArtNetData = false,
                         in bool dmx_TextPacketsSupported = false,
                         in bool dmx_SIPsSupported = false,
                         in bool dmx_TestPacketsSupported = false,
                         in bool isBeingOutputAsDMX = false,
                         in EOutputStyle outputStyle = EOutputStyle.Delta,
                         in bool rdmIsDisabled = false,
                         in bool discoveryIsCurrentlyRunning = false,
                         in bool backgroundDiscoveryIsEnabled = false) : this()
        {
            ConvertFrom = convertFrom;
            MergeMode = mergeMode;
            DMX_OutputShortCircuit = dmx_OutputShortCircuit;
            MergingArtNetData = mergingArtNetData;
            DMX_TextPacketsSupported = dmx_TextPacketsSupported;
            DMX_SIPsSupported = dmx_SIPsSupported;
            DMX_TestPacketsSupported = dmx_TestPacketsSupported;
            IsBeingOutputAsDMX = isBeingOutputAsDMX;
            OutputStyle = outputStyle;
            RDMisDisabled = rdmIsDisabled;
            DiscoveryIsCurrentlyRunning = discoveryIsCurrentlyRunning;
            BackgroundDiscoveryIsEnabled = backgroundDiscoveryIsEnabled;

            Byte1 |= (byte)ConvertFrom;
            Byte1 |= (byte)MergeMode;

            // Calculate Byte 1
            if (DMX_OutputShortCircuit)
                Byte1 |= 0b00000100;
            if (MergingArtNetData)
                Byte1 |= 0b00001000;
            if (DMX_TextPacketsSupported)
                Byte1 |= 0b00010000;
            if (DMX_SIPsSupported)
                Byte1 |= 0b00100000;
            if (DMX_TestPacketsSupported)
                Byte1 |= 0b01000000;
            if (IsBeingOutputAsDMX)
                Byte1 |= 0b10000000;


            Byte2 |= (byte)OutputStyle;
            if (RDMisDisabled)
                Byte2 |= 0b10000000;
            if (DiscoveryIsCurrentlyRunning)
                Byte2 |= 0b00100000;
            if (BackgroundDiscoveryIsEnabled)
                Byte2 |= 0b00010000;
        }

        public static GoodOutput operator |(in GoodOutput goodOutputA, in GoodOutput goodOutputB)
        {
            byte byte1 = (byte)(goodOutputA.Byte1 | goodOutputB.Byte1);
            byte byte2 = (byte)(goodOutputA.Byte2 | goodOutputB.Byte2);
            return new GoodOutput(byte1, byte2);
        }
        public static GoodOutput operator &(in GoodOutput goodOutputA, in GoodOutput goodOutputB)
        {
            byte byte1 = (byte)(goodOutputA.Byte1 & goodOutputB.Byte1);
            byte byte2 = (byte)(goodOutputA.Byte2 & goodOutputB.Byte2);
            return new GoodOutput(byte1, byte2);
        }
        public static GoodOutput operator ~(in GoodOutput goodOutput)
        {
            byte byte1 = (byte)~goodOutput.Byte1;
            byte byte2 = (byte)~goodOutput.Byte2;
            return new GoodOutput(byte1, byte2);
        }

        public static bool operator ==(in GoodOutput left, in GoodOutput right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in GoodOutput left, in GoodOutput right)
        {
            return !(left == right);
        }
        public enum EConvertFrom : byte
        {
            ArtNet = 0b00000000,
            sACN = 0b00000001,
        }
        public enum EOutputStyle : byte
        {
            Continuous = 0b01000000,
            Delta = 0b00000000,
        }
        public static implicit operator ushort(in GoodOutput goodOutput)
        {
            return (ushort)(goodOutput.Byte1 + (goodOutput.Byte2 << 8));
        }
        public static implicit operator GoodOutput(in ushort b)
        {
            return new GoodOutput((byte)(b & 0xff), (byte)(b >> 8 & 0xff));
        }


        public override bool Equals(object obj)
        {
            return obj is GoodOutput status && Equals(status);
        }

        public bool Equals(GoodOutput other)
        {
            return Byte1 == other.Byte1;
        }

        public override int GetHashCode()
        {
            int hashCode = -971048872;
            hashCode = hashCode * -1521134295 + Byte1.GetHashCode();
            hashCode = hashCode * -1521134295 + Byte2.GetHashCode();
            return hashCode;
        }
    }
}