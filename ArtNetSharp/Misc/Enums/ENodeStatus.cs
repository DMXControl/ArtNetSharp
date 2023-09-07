using System;
using System.Xml.Linq;

namespace ArtNetSharp
{
    [Flags]
    public enum ENodeStatus : uint
    {
        None = 0,
        /// <summary>
        /// 0 = UBEA not present or corrupt.
        /// 1 = UBEA present.
        /// </summary>
        UBEA_Present = 0b000000000000000000000001,
        /// <summary>
        /// 0 = Not capable of Remote Device Management (RDM).
        /// 1 = Capable of Remote Device Management (RDM).
        /// </summary>
        RDM_Supported = 0b000000000000000000000010,
        /// <summary>
        /// 0 = Normal firmware boot (from flash). Nodes that do not support dual boot, clear this field to zero.
        /// 1 = Booted from ROM
        /// </summary>
        ROM_Booted = 0b000000000000000000000100,

        //NOT_SET = 0b000000000000000000001000,

        /// <summary>
        /// Port-Address Programming Authority
        /// All Port-Address set by front panel controls.
        /// </summary>
        PortAddressProgrammingAuthorityByFrontPanel = 0b000000000000000000010000,
        /// <summary>
        /// Port-Address Programming Authority
        /// All or part of Port-Address programmed by network or Web browser.
        /// </summary>
        PortAddressProgrammingAuthorityByNetwork = 0b000000000000000000100000,
        /// <summary>
        /// Port-Address Programming Authority
        /// Not used.
        /// </summary>
        PortAddressProgrammingAuthorityNotUsed = 0b000000000000000000110000,

        /// <summary>
        /// Indicators in Locate / Identify Mode
        /// </summary>
        IndicatorStateLocate = 0b000000000000000001000000,
        /// <summary>
        /// Indicators in Mute Mode.
        /// </summary>
        IndicatorStateMute = 0b000000000000000010000000,
        /// <summary>
        /// Indicators in Normal Mode.
        /// </summary>
        IndicatorStateNormal = 0b000000000000000011000000,

        /// <summary>
        /// Set = Product supports web browser configuration.
        /// </summary>
        WebConfigurationSupported = 0b000000000000000100000000,
        /// <summary>
        /// Clr = Node’s IP is manually configured.
        /// Set = Node’s IP is DHCP configured.
        /// </summary>
        DHCP_ConfigurationUsed = 0b000000000000001000000000,
        /// <summary>
        /// Clr = Node is not DHCP capable.
        /// Set = Node is DHCP capable.
        /// </summary>
        DHCP_ConfigurationSupported = 0b000000000000010000000000,
        /// <summary>
        /// Clr = Node supports 8-bit Port-Address (Art-Net II).
        /// Set = Node supports 15-bit Port-Address (Art-Net 3 or 4).
        /// </summary>
        NodeSupports15BitPortAddress = 0b000000000000100000000000,
        /// <summary>
        /// Clr = Node not able to switch between Art-Net and sACN.
        /// Set = Node is able to switch between Art-Net and sACN.
        /// </summary>
        NodeSupportArtNet_sACN_Switching = 0b000000000001000000000000,
        /// <summary>
        /// Clr = Not squawking.
        /// Set = squawking.
        /// </summary>
        Squawking = 0b000000000010000000000000,
        /// <summary>
        /// Clr = Node does not support switching of output style using ArtCommand.
        /// Set = Node supports switching of output style using ArtCommand.
        /// </summary>
        NodeSupportOutputStyleSwitching = 0b000000000100000000000000,
        /// <summary>
        /// Clr = Node does not support control of RDM using ArtCommand.
        /// Set = Node supports control of RDM using ArtCommand.
        /// </summary>
        NodeSupportRDM_Switching = 0b000000001000000000000000,

        //q = 0b000000010000000000000000,
        //r = 0b000000100000000000000000,
        //s = 0b000001000000000000000000,
        /// <summary>
        /// Set – Node supports switching ports between input and output. (PortTypes[] shows the current direction).
        /// Clr – Node does not support switching port direction.
        /// </summary>
        NodeSupportSwitchingBetweenInputOutput = 0b000010000000000000000000,
        /// <summary>
        /// Set – Node supports LLRP.
        /// Clr – Node does not support LLRP.
        /// </summary>
        NodeSupportLLRP = 0b000100000000000000000000,
        /// <summary>
        /// Set – Node supports fail-over.
        /// Clr – Node does not support fail-over.
        /// </summary>
        NodeSupportFailOver = 0b001000000000000000000000,

        /// <summary>
        /// 01 All outputs to zero.
        /// </summary>
        FailsafeStateAllZero = 0b010000000000000000000000,
        /// <summary>
        /// 10 All outputs to full.
        /// </summary>
        FailsafeStateAllFull = 0b100000000000000000000000,
        /// <summary>
        /// 11 Playback fail safe scene.
        /// </summary>
        FailsafeStatePlaybackScene = 0b110000000000000000000000,
    }
}