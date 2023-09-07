using System;

namespace ArtNetSharp
{
    [Flags]
    public enum EArtIpProgCommand : byte
    {
        None=0,
        ProgramPort              = 0b00000001,
        ProgramSubnetMask        = 0b00000010,
        ProgramIP                = 0b00000100,
        ResetParametersToDefault = 0b00001000,
        ResetDefaultGateway      = 0b00010000,
        // Not Used              = 0b00100000,
        EnableDHCP               = 0b01000000,
        EnableProgramming        = 0b10000000,
    }
}