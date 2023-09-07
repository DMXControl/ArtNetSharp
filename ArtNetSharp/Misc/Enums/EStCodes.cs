namespace ArtNetSharp
{
    /// <summary>
    /// The Style code defines the general
    /// functionality of a Controller.The Style code is returned in ArtPollReply.
    /// </summary>
    public enum EStCodes : ushort
    {
        /// <summary>
        /// A DMX to / from Art-Net device
        /// </summary>
        StNode = 0x00,
        /// <summary>
        /// A lighting console.
        /// </summary>
        StController = 0x01,
        /// <summary>
        /// A Media Server.
        /// </summary>
        StMedia = 0x02,
        /// <summary>
        /// A network routing device.
        /// </summary>
        StRoute = 0x03,
        /// <summary>
        /// A backup device.
        /// </summary>
        StBackup = 0x04,
        /// <summary>
        /// A configuration or diagnostic tool.
        /// </summary>
        StConfig = 0x05,
        /// <summary>
        /// A visualiser.
        /// </summary>
        StVisual = 0x06,
    }
}