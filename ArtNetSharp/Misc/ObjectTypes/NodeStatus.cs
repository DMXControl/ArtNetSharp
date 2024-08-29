using System;

namespace ArtNetSharp
{
    public readonly struct NodeStatus : IEquatable<NodeStatus>
    {
        public static readonly NodeStatus None = new NodeStatus(0, 0, 0);
        public static readonly NodeStatus NodeSupports15BitPortAddress = new NodeStatus(portAddressBitResolution: EPortAddressBitResolution._15Bit);
        public static readonly NodeStatus RDMSupported = new NodeStatus(rDM_Supported: true);
        public readonly byte StatusByte1;
        public readonly byte StatusByte2;
        public readonly byte StatusByte3;

        /// <summary>
        /// false = UBEA not present or corrupt.
        /// true = UBEA present.
        /// </summary>
        public readonly bool UBEA_Present;
        /// <summary>
        /// false = Not capable of Remote Device Management (RDM).
        /// true = Capable of Remote Device Management (RDM).
        /// </summary>
        public readonly bool RDM_Supported;
        /// <summary>
        /// false = Normal firmware boot (from flash). Nodes that do not support dual boot, clear this field to zero.
        /// true = Booted from ROM
        /// </summary>
        public readonly bool ROM_Booted;

        /// <summary>
        /// Port-Address Programming Authority
        /// 01 All Port-Address set by front panel controls.
        /// 10 All or part of Port-Address programmed by network or Web browser.
        /// 11 Not used.
        /// </summary>
        public readonly EPortAddressProgrammingAuthority PortAddressProgrammingAuthority;

        /// <summary>
        /// Indicators State Unknown
        /// Indicators in Locate / Identify Mode
        /// Indicators in Mute Mode.
        /// Indicators in Normal Mode.
        /// </summary>
        public readonly EIndicatorState IndicatorState;

        /// <summary>
        /// Set = Product supports web browser configuration.
        /// </summary>
        public readonly bool WebConfigurationSupported;
        /// <summary>
        /// Clr = Node’s IP is manually configured.
        /// Set = Node’s IP is DHCP configured.
        /// </summary>
        public readonly bool DHCP_ConfigurationUsed;
        /// <summary>
        /// Clr = Node is not DHCP capable.
        /// Set = Node is DHCP capable.
        /// </summary>
        public readonly bool DHCP_ConfigurationSupported;
        /// <summary>
        /// Clr = Node supports 8-bit Port-Address (Art-Net II).
        /// Set = Node supports 15-bit Port-Address (Art-Net 3 or 4).
        /// </summary>
        public readonly EPortAddressBitResolution PortAddressBitResolution;
        /// <summary>
        /// Clr = Node not able to switch between Art-Net and sACN.
        /// Set = Node is able to switch between Art-Net and sACN.
        /// </summary>
        public readonly bool NodeSupportArtNet_sACN_Switching;
        /// <summary>
        /// Clr = Not squawking.
        /// Set = squawking.
        /// </summary>
        public readonly bool Squawking;
        /// <summary>
        /// Clr = Node does not support switching of output style using ArtCommand.
        /// Set = Node supports switching of output style using ArtCommand.
        /// </summary>
        public readonly bool NodeSupportOutputStyleSwitching;
        /// <summary>
        /// Clr = Node does not support control of RDM using ArtCommand.
        /// Set = Node supports control of RDM using ArtCommand.
        /// </summary>
        public readonly bool NodeSupportRDM_Switching;

        //q = 0b000000010000000000000000,
        //r = 0b000000100000000000000000,
        //s = 0b000001000000000000000000,
        /// <summary>
        /// Set – Node supports switching ports between input and output. (PortTypes[] shows the current direction).
        /// Clr – Node does not support switching port direction.
        /// </summary>
        public readonly bool NodeSupportSwitchingBetweenInputOutput;
        /// <summary>
        /// Set – Node supports LLRP.
        /// Clr – Node does not support LLRP.
        /// </summary>
        public readonly bool NodeSupportLLRP;
        /// <summary>
        /// Set – Node supports fail-over.
        /// Clr – Node does not support fail-over.
        /// </summary>
        public readonly bool NodeSupportFailOver;

        /// <summary>
        /// 00 Hold last state.
        /// 01 All outputs to zero.
        /// 10 All outputs to full.
        /// 11 Playback fail safe scene.
        /// </summary>
        public readonly EFailsafeState FailsafeState;

        /// <summary>
        /// Set – BackgroundQueue is supported
        /// Clr – BackgroundQueue is not supported
        /// </summary>
        public readonly bool BackgroundQueueSupported;

        /// <summary>
        /// Set – Programmable background discovery is supported.
        /// Clr – Programmable background discovery is not supported
        /// </summary>
        public readonly bool ProgrammableBackgroundDiscoverySupported;


        public NodeStatus(in byte statusByte1, in byte statusByte2, in byte statusByte3)
        {
            StatusByte1 = statusByte1;
            StatusByte2 = statusByte2;
            StatusByte3 = statusByte3;

            //Status 1
            UBEA_Present = Tools.BitsMatch(StatusByte1, 0b00000001);
            RDM_Supported = Tools.BitsMatch(StatusByte1, 0b00000010);
            ROM_Booted = Tools.BitsMatch(StatusByte1, 0b00000100);

            //NOT_SET                                             Tools.BitsMatch(StatusByte1, 0b00001000);

            PortAddressProgrammingAuthority = (EPortAddressProgrammingAuthority)(StatusByte1 & 0b00110000);

            IndicatorState = (EIndicatorState)(StatusByte1 & 0b11000000);

            //Status 2
            WebConfigurationSupported = Tools.BitsMatch(StatusByte2, 0b00000001);
            DHCP_ConfigurationUsed = Tools.BitsMatch(StatusByte2, 0b00000010);
            DHCP_ConfigurationSupported = Tools.BitsMatch(StatusByte2, 0b00000100);

            PortAddressBitResolution = (EPortAddressBitResolution)(StatusByte2 & 0b00001000);
            NodeSupportArtNet_sACN_Switching = Tools.BitsMatch(StatusByte2, 0b00010000);

            Squawking = Tools.BitsMatch(StatusByte2, 0b00100000);
            NodeSupportOutputStyleSwitching = Tools.BitsMatch(StatusByte2, 0b01000000);
            NodeSupportRDM_Switching = Tools.BitsMatch(StatusByte2, 0b10000000);

            // Status 3
            NodeSupportSwitchingBetweenInputOutput = Tools.BitsMatch(StatusByte3, 0b00001000);
            NodeSupportLLRP = Tools.BitsMatch(StatusByte3, 0b00010000);
            NodeSupportFailOver = Tools.BitsMatch(StatusByte3, 0b00100000);
            FailsafeState = (EFailsafeState)(StatusByte3 & 0b11000000);
            BackgroundQueueSupported = Tools.BitsMatch(StatusByte3, 0b00000010);
            ProgrammableBackgroundDiscoverySupported = Tools.BitsMatch(StatusByte3, 0b00000001);
        }

        public NodeStatus(in bool uBEA_Present = false,
                          in bool rDM_Supported = false,
                          in bool rOM_Booted = false,
                          in EPortAddressProgrammingAuthority portAddressProgrammingAuthority = EPortAddressProgrammingAuthority.Unknown,
                          in EIndicatorState indicatorState = EIndicatorState.Unknown,
                          in bool webConfigurationSupported = false,
                          in bool dHCP_ConfigurationUsed = false,
                          in bool dHCP_ConfigurationSupported = false,
                          in EPortAddressBitResolution portAddressBitResolution = EPortAddressBitResolution._15Bit,
                          in bool nodeSupportArtNet_sACN_Switching = false,
                          in bool squawking = false,
                          in bool nodeSupportOutputStyleSwitching = false,
                          in bool nodeSupportRDM_Switching = false,
                          in bool nodeSupportSwitchingBetweenInputOutput = false,
                          in bool nodeSupportLLRP = false,
                          in bool nodeSupportFailOver = false,
                          in EFailsafeState failsafeState = EFailsafeState.Hold,
                          in bool backgroundQueueSupported = false,
                          in bool programmableBackgroundDiscoverySupported = false) : this()
        {

            UBEA_Present = uBEA_Present;
            RDM_Supported = rDM_Supported;
            ROM_Booted = rOM_Booted;
            PortAddressProgrammingAuthority = portAddressProgrammingAuthority;
            IndicatorState = indicatorState;
            WebConfigurationSupported = webConfigurationSupported;
            DHCP_ConfigurationUsed = dHCP_ConfigurationUsed;
            DHCP_ConfigurationSupported = dHCP_ConfigurationSupported;
            PortAddressBitResolution = portAddressBitResolution;
            NodeSupportArtNet_sACN_Switching = nodeSupportArtNet_sACN_Switching;
            Squawking = squawking;
            NodeSupportOutputStyleSwitching = nodeSupportOutputStyleSwitching;
            NodeSupportRDM_Switching = nodeSupportRDM_Switching;
            NodeSupportSwitchingBetweenInputOutput = nodeSupportSwitchingBetweenInputOutput;
            NodeSupportLLRP = nodeSupportLLRP;
            NodeSupportFailOver = nodeSupportFailOver;
            FailsafeState = failsafeState;
            BackgroundQueueSupported = backgroundQueueSupported;
            ProgrammableBackgroundDiscoverySupported = programmableBackgroundDiscoverySupported;

            // Calculate StatusByte 1
            if (UBEA_Present)
                StatusByte1 |= 0b00000001;
            if (RDM_Supported)
                StatusByte1 |= 0b00000010;
            if (ROM_Booted)
                StatusByte1 |= 0b00000100;

            StatusByte1 |= (byte)PortAddressProgrammingAuthority;
            StatusByte1 |= (byte)IndicatorState;

            // Calculate StatusByte 2
            if (WebConfigurationSupported)
                StatusByte2 |= 0b00000001;
            if (DHCP_ConfigurationUsed)
                StatusByte2 |= 0b00000010;
            if (DHCP_ConfigurationSupported)
                StatusByte2 |= 0b00000100;

            StatusByte2 |= (byte)PortAddressBitResolution;

            if (NodeSupportArtNet_sACN_Switching)
                StatusByte2 |= 0b00010000;
            if (Squawking)
                StatusByte2 |= 0b00100000;
            if (NodeSupportOutputStyleSwitching)
                StatusByte2 |= 0b01000000;
            if (NodeSupportRDM_Switching)
                StatusByte2 |= 0b10000000;

            // Calculate StatusByte 3

            if (NodeSupportSwitchingBetweenInputOutput)
                StatusByte3 |= 0b00001000;
            if (NodeSupportLLRP)
                StatusByte3 |= 0b00010000;
            if (NodeSupportFailOver)
                StatusByte3 |= 0b00100000;

            StatusByte3 |= (byte)FailsafeState;

            if (BackgroundQueueSupported)
                StatusByte3 |= 0b00000010;
            if (ProgrammableBackgroundDiscoverySupported)
                StatusByte3 |= 0b00000001;
        }
        public static NodeStatus operator |(NodeStatus nodeStatusA, NodeStatus nodeStatusB)
        {
            byte statusByte1 = (byte)(nodeStatusA.StatusByte1 | nodeStatusB.StatusByte1);
            byte statusByte2 = (byte)(nodeStatusA.StatusByte2 | nodeStatusB.StatusByte2);
            byte statusByte3 = (byte)(nodeStatusA.StatusByte3 | nodeStatusB.StatusByte3);
            return new NodeStatus(statusByte1, statusByte2, statusByte3);
        }
        public static NodeStatus operator &(NodeStatus nodeStatusA, NodeStatus nodeStatusB)
        {
            byte statusByte1 = (byte)(nodeStatusA.StatusByte1 & nodeStatusB.StatusByte1);
            byte statusByte2 = (byte)(nodeStatusA.StatusByte2 & nodeStatusB.StatusByte2);
            byte statusByte3 = (byte)(nodeStatusA.StatusByte3 & nodeStatusB.StatusByte3);
            return new NodeStatus(statusByte1, statusByte2, statusByte3);
        }
        public static NodeStatus operator ~(NodeStatus nodeStatus)
        {
            byte statusByte1 = (byte)(~nodeStatus.StatusByte1);
            byte statusByte2 = (byte)(~nodeStatus.StatusByte2);
            byte statusByte3 = (byte)(~nodeStatus.StatusByte3);
            return new NodeStatus(statusByte1, statusByte2, statusByte3);
        }

        public static bool operator ==(NodeStatus left, NodeStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NodeStatus left, NodeStatus right)
        {
            return !(left == right);
        }

        public enum EPortAddressBitResolution : byte
        {
            _8Bit = 0b00000000,
            _15Bit = 0b00001000,
        }
        public enum EPortAddressProgrammingAuthority : byte
        {
            Unknown = 0b00000000,
            ByFrontPanel = 0b00010000,
            ByNetwork = 0b00100000,
            NotUsed = 0b00110000,
        }
        public enum EIndicatorState : byte
        {
            Unknown = 0b00000000,
            Locate = 0b01000000,
            Mute = 0b10000000,
            Normal = 0b11000000
        }
        public enum EFailsafeState : byte
        {
            Hold = 0b00000000,
            AllZero = 0b01000000,
            AllFull = 0b10000000,
            PlaybackScene = 0b11000000
        }

        public override bool Equals(object obj)
        {
            return obj is NodeStatus status && Equals(status);
        }

        public bool Equals(NodeStatus other)
        {
            return StatusByte1 == other.StatusByte1 &&
                   StatusByte2 == other.StatusByte2 &&
                   StatusByte3 == other.StatusByte3;
        }

        public override int GetHashCode()
        {
            int hashCode = -971048872;
            hashCode = hashCode * -1521134295 + StatusByte1.GetHashCode();
            hashCode = hashCode * -1521134295 + StatusByte2.GetHashCode();
            hashCode = hashCode * -1521134295 + StatusByte3.GetHashCode();
            return hashCode;
        }
    }
}