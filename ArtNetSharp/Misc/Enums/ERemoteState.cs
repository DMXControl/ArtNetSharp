using System;

namespace ArtNetSharp
{
    [Flags]
    public enum ERemoteState : byte
    {
        None = 0,
        Remote1Active = 0b00000001,
        Remote2Active = 0b00000010,
        Remote3Active = 0b00000100,
        Remote4Active = 0b00001000,
        Remote5Active = 0b00010000,
        Remote6Active = 0b00100000,
        Remote7Active = 0b01000000,
        Remote8Active = 0b10000000,
    }
}