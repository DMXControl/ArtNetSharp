namespace ArtNetSharp
{
    public enum EPayloadLanguageCode : ushort
    {
        /// <summary>
        /// Payload contains a simple text string representing a URL.
        /// </summary>
        BeaconURL = 0x0000,
        /// <summary>
        /// – Payload contains a simple ASCII text message.
        /// </summary>
        BeaconText = 0x0001,
        /// <summary>
        /// – Payload contains a big-endian 16-bit number.
        /// </summary>
        BeaconLocationID = 0x0002,
    }
}