using System;

namespace ArtNetSharp
{
    [Flags]
    public enum EArtIpProgReplyStatusFlags : byte
    {
        None=0,
        EnableDHCP               = 0b01000000,
    }
}