namespace ArtNetSharp
{
    public enum EArtTodControlCommand : byte
    {
        /// <summary>
        /// No action.
        /// </summary>
        AtcNone = 0x00,
        /// <summary>
        /// The port flushes its TOD
        /// and instigates full
        /// discovery.
        /// </summary>
        AtcFlush = 0x01,
        /// <summary>
        /// The port ends current discovery but does not flush ToD.
        /// </summary>
        AtcEnd = 0x02,
        /// <summary>
        /// The port enables incremental discovery.
        /// </summary>
        AtcIncOn = 0x03,
        /// <summary>
        /// The port disables incremental discovery.
        /// </summary>
        AtcIncOff = 0x04
    }
}