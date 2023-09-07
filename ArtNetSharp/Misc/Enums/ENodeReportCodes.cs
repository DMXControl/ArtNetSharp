namespace ArtNetSharp
{
    public enum ENodeReportCodes : ushort
    {
        /// <summary>
        /// Booted in debug mode (Only used in development).
        /// </summary>
        RcDebug = 0x0000,
        /// <summary>
        /// Power On Tests successful.
        /// </summary>
        RcPowerOk = 0x0001,
        /// <summary>
        /// Hardware tests failed at Power On.
        /// </summary>
        RcPowerFail = 0x0002,
        /// <summary>
        /// Last UDP from Node failed due to truncated length, Most likely caused by a collision.
        /// </summary>
        RcSocketWr1 = 0x0003,
        /// <summary>
        /// Unable to identify last UDP transmission. Check OpCode and packet length.
        /// </summary>
        RcParseFail = 0x0004,
        /// <summary>
        /// Unable to open Udp Socket in last transmission attempt.
        /// </summary>
        RcUdpFail = 0x0005,
        /// <summary>
        /// Confirms that Short Name programming via ArtAddress, was successful.
        /// </summary>
        RcShNameOk = 0x0006,
        /// <summary>
        /// Confirms that Long Name programming via ArtAddress, was successful.
        /// </summary>
        RcLoNameOk = 0x0007,
        /// <summary>
        /// DMX512 receive errors detected.
        /// </summary>
        RcDmxError = 0x0008,
        /// <summary>
        /// Ran out of internal DMX transmit buffers.
        /// </summary>
        RcDmxUdpFull = 0x0009,
        /// <summary>
        /// Ran out of internal DMX Rx buffers.
        /// </summary>
        RcDmxRxFull = 0x000a,
        /// <summary>
        /// Rx Universe switches conflict.
        /// </summary>
        RcSwitchErr = 0x000b,
        /// <summary>
        /// Product configuration does not match firmware.
        /// </summary>
        RcConfigErr = 0x000c,
        /// <summary>
        /// DMX output short detected. See GoodOutput field.
        /// </summary>
        RcDmxShort = 0x000d,
        /// <summary>
        /// Last attempt to upload new firmware failed.
        /// </summary>
        RcFirmwareFail = 0x000e,
        /// <summary>
        /// User changed switch settings when address locked by remote programming. User changes ignored.
        /// </summary>
        RcUserFail = 0x000f,
        /// <summary>
        /// Factory reset has occurred.
        /// </summary>
        RcFactoryRes = 0x0010
    }
}