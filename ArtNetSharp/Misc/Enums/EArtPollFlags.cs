using System;

namespace ArtNetSharp
{
    [Flags]
    public enum EArtPollFlags : byte
    {
        None = 0,
        ArtPollReplyOnChange = 0b00000010,
        DiagnosticEnabled = 0b00000100,
        DiagnosticUnicast = 0b00001000,
        DisableVLCtransmission = 0b00010000,
        EnableTargetedMode = 0b00100000,
    }
}