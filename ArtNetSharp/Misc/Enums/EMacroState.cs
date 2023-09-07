using System;

namespace ArtNetSharp
{
    [Flags]
    public enum EMacroState : byte
    {
        None=0,
        Macro1Active = 0b00000001,
        Macro2Active = 0b00000010,
        Macro3Active = 0b00000100,
        Macro4Active = 0b00001000,
        Macro5Active = 0b00010000,
        Macro6Active = 0b00100000,
        Macro7Active = 0b01000000,
        Macro8Active = 0b10000000,
    }
}