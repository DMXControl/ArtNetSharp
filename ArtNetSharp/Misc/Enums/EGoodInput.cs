using System;

namespace ArtNetSharp
{
    [Flags]
    public enum EGoodInput : byte
    {
        None = 0,
        ReceiveErrorsDetected = 0b00000100,
        InputIsDisabled = 0b00001000,
        DMX_TestPacketsSupported = 0b00010000,
        DMX_SIPsSupported = 0b00100000,
        DMX_TestPacketsSupported2 = 0b01000000,
        DataReceived = 0b10000000,
    }
}