using System;

namespace ArtNetSharp
{
    [Flags]
    public enum EGoodOutput : ushort //Todo refactor to readonly struckt
    {
        OutputFrom_ArtNet = 0b00000000,
        OutputFrom_sACN = 0b00000001,
        MergeModeIsLTP = 0b00000010,
        DMX_OutputShortCircuit = 0b00000100,
        MergingArtNetData = 0b00001000,
        DMX_TestPacketsSupported = 0b00010000,
        DMX_SIPsSupported = 0b00100000,
        DMX_TestPacketsSupported2 = 0b01000000,
        DataTransmitted = 0b10000000,

        ContiniuousOutput = 0b0100000000000000,
        RDMisDisabled = 0b1000000000000000,
    }
}