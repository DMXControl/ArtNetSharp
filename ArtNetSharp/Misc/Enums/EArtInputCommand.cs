using System;

namespace ArtNetSharp
{
    [Flags]
    public enum EArtInputCommand : byte
    {
        None = 0x00000000,
        /// <summary>
        /// Set to disable this input
        /// </summary>
        DisableInput = 0x00000001
    }
}