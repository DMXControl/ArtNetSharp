using System;

namespace ArtNetSharp.Misc
{
    /// <summary>
    /// An ArtAddress was received (remote reprogramming of this node). Raised before the library applies it.
    /// Set <see cref="Cancel"/> to reject the change; the node then only answers with its unchanged ArtPollReply.
    /// <para>
    /// The library applies itself: Port Name (ShortName), Long Name, Net/Sub-Net/SwIn/SwOut of the ports with the given
    /// BindIndex, the merge mode (AcMergeLtp/AcMergeHtp) and AcCancelMerge. All other commands (LED, fail-safe,
    /// direction, style, RDM, sACN, ...) are only reported here for the application to act on.
    /// </para>
    /// </summary>
    public class ArtAddressReceivedEventArgs : EventArgs
    {
        public readonly ArtAddress ArtAddress;
        public readonly IPv4Address Source;
        /// <summary>Set to true to reject the reprogramming.</summary>
        public bool Cancel { get; set; }

        public ArtAddressReceivedEventArgs(ArtAddress artAddress, IPv4Address source)
        {
            ArtAddress = artAddress;
            Source = source;
        }
    }

    /// <summary>
    /// An ArtIpProg was received (remote reprogramming of the IP settings). The library cannot change the network
    /// configuration of the host. An application that can (e.g. an embedded device) applies the request and fills in
    /// the resulting settings; they are reported back in the ArtIpProgReply. Without a handler the current settings are
    /// reported unchanged.
    /// </summary>
    public class ArtIpProgReceivedEventArgs : EventArgs
    {
        public readonly ArtIpProg Request;
        public readonly IPv4Address Source;

        /// <summary>Settings reported in the ArtIpProgReply. Pre-filled with the current settings of the receiving interface.</summary>
        public IPv4Address Ip { get; set; }
        public IPv4Address SubnetMask { get; set; }
        public IPv4Address DefaultGateway { get; set; }
        public bool DhcpEnabled { get; set; }

        public ArtIpProgReceivedEventArgs(ArtIpProg request, IPv4Address source, IPv4Address ip, IPv4Address subnetMask, IPv4Address defaultGateway, bool dhcpEnabled)
        {
            Request = request;
            Source = source;
            Ip = ip;
            SubnetMask = subnetMask;
            DefaultGateway = defaultGateway;
            DhcpEnabled = dhcpEnabled;
        }
    }
}
