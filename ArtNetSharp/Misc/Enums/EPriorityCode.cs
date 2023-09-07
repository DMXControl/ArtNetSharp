namespace ArtNetSharp
{
    public enum EPriorityCode : byte
    {
        None = 0,
        /// <summary>
        /// Low priority message.
        /// </summary>
        DpLow = 0x10,
        /// <summary>
        /// Medium priority message
        /// </summary>
        DpMed = 0x40,
        /// <summary>
        /// High priority message.
        /// </summary>
        DpHigh = 0x80,
        /// <summary>
        /// Critical priority message.
        /// </summary>
        DpCritical = 0xe0,
        /// <summary>
        /// Volatile message. Messages of this type are displayed
        /// on a single line in the DMX-Workshopdiagnostics
        /// display. All other types are displayed in a list box.
        /// </summary>
        DpVolatile = 0xf0,
    }
}