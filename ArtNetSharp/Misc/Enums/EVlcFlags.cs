using System;

namespace ArtNetSharp
{
    [Flags]
    public enum EVlcFlags : byte
    {
        None = 0b00000000,
        /// <summary>
        /// If set, the transmitter should continuously repeat transmission of this packet until another is received.
        /// If clear, the transmitter should transmit this packet once.
        /// </summary>
        Beacon = 0b00100000,
        /// <summary>
        /// If set this is a reply packet that is in response to the request sent with matching number in thetransaction number:
        /// TransHi/Lo. If clear this is not a reply.
        /// </summary>
        Reply = 0b01000000,
        /// <summary>
        /// If set, data in the payload area shall be interpreted as IEEE VLC data.
        /// If clear, PayLanguage defines the payload contents.
        /// </summary>
        IEEE = 0b10000000,
    }
}