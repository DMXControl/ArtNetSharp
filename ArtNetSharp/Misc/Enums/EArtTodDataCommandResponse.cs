namespace ArtNetSharp
{
    public enum EArtTodDataCommandResponse : byte
    {
        /// <summary>
        /// The packet contains the entire
        /// TOD or isthe first packet in a
        /// sequence of packets that
        /// contains the entire TOD.
        /// </summary>
        TodFull = 0x00,
        /// <summary>
        /// The TOD is not available or
        /// discovery is incomplete.
        /// </summary>
        TodNak = 0xff
    }
}