namespace ArtNetSharp
{
    public enum ERDMVersion : byte
    {
        /// <summary>
        /// Devices that only support RDM DRAFT V1.0
        /// </summary>
        DRAFT_V1_0 = 0x00,
        /// <summary>
        /// Devices that support RDM STANDARD V1.0
        /// </summary>
        STANDARD_V1_0 = 0x01
    }
}