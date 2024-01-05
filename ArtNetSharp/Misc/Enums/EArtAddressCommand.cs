namespace ArtNetSharp
{
    public enum EArtAddressCommand : byte
    {
        None = 0x00,
        /// <summary>
        ///  If Node is currently in merge mode, cancel merge mode upon receipt of next ArtDmx packet.
        ///  See discussion of merge mode operation.
        /// </summary>
        CancelMerge = 0x01,
        /// <summary>
        /// The front panel indicators of the Node operate normally.
        /// </summary>
        LedNormal = 0x02,
        /// <summary>
        /// The front panel indicators of the Node are disabled and switched off.
        /// </summary>
        LedMute = 0x03,
        /// <summary>
        /// Rapid flashing of the Node’s front panel indicators. It is intended as an outlet identifierfor large installations.
        /// </summary>
        LedLocate = 0x04,
        /// <summary>
        /// Resets the Node’s Sip, Text, Test and data error flags.
        /// If an output short is being flagged, forcesthe test to re-run.
        /// </summary>
        ResetRxFlags = 0x05,
        /// <summary>
        /// Enable analysis and debugging mode.
        /// </summary>
        AnalysisOn = 0x06,
        /// <summary>
        /// Disable analysis and debugging mode.
        /// </summary>
        AnalysisOff = 0x07,

        //Failsafe configuration commands: These settings should be retained by the nodeduring power cycling.

        /// <summary>
        /// Set the node to hold last state in the event of loss of network data.
        /// </summary>
        FailHold = 0x09,
        /// <summary>
        /// Set the node’s outputs to zero in the event of loss of network data.
        /// </summary>
        FailZero = 0x0a,
        /// <summary>
        /// Set the node’s outputs to full in the event of loss of network data.
        /// </summary>
        FailFull = 0x0b,
        /// <summary>
        /// Set the node’s outputs to play the failsafescene in the event of loss of network data.
        /// </summary>
        FailScene = 0x0c,
        /// <summary>
        /// Record the current output state as the failsafescene.
        /// </summary>
        FailRecord = 0x0d,

        //Node configuration commands: Note that Ltp / Htp and directionsettings should be retained by the node during power cycling.

        /// <summary>
        /// Set DMX Port x to Merge in LTP mode.
        /// </summary>
        MergeLtp = 0x10,
        /// <summary>
        /// Set Port x direction to output.
        /// </summary>
        DirectionTx = 0x20,
        /// <summary>
        /// Set Port x direction to input.
        /// </summary>
        DirectionRx = 0x30,
        /// <summary>
        /// Set DMX Port x to Merge in HTP (default) mode.
        /// </summary>
        MergeHtp = 0x50,
        /// <summary>
        /// Set DMX Port x to output both DMX512 and RDM packets from the Art-Net protocol (default).
        /// </summary>
        ArtNetSel = 0x60,
        /// <summary>
        /// Set DMX Port x to output DMX512 data from the sACN protocol and RDM data from the Art-Net protocol.
        /// </summary>
        AcnSel = 0x70,
        /// <summary>
        /// Clear DMX Output buffer for Port x
        /// </summary>
        ClearOp = 0x90,
        /// <summary>
        /// Set output style to delta mode (DMX frame triggered by ArtDmx) for Port x
        /// </summary>
        StyleDelta = 0xa0,
        /// <summary>
        /// Set output style to constant mode (DMX output is continuous) for Port x
        /// </summary>
        StyleConst = 0xb0,
        /// <summary>
        /// Enable RDM for Port x
        /// </summary>
        RdmEnable = 0xc0,
        /// <summary>
        /// Disable RDM for Port x
        /// </summary>
        RdmDisable = 0xd0,
    }
}