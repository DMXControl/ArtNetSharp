using System;

namespace ArtNetSharp
{
    [Flags]
    public enum EPortType : byte
    {
        DMX512 = 0b00000000,
        MIDI = 0b00000001,
        Avab = 0b00000010,
        ColortranCMX = 0b00000011,
        ADB62dot5 = 0b00000100,
        ArtNet = 0b00000101,
        DALI = 0b00000110,

        InputToArtNet = 0b01000000,
        OutputFromArtNet = 0b10000000,
    }
}