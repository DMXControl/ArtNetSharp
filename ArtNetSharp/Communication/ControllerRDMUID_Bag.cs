using RDMSharp;
using System;

namespace ArtNetSharp.Communication
{
    public sealed class ControllerRDMUID_Bag
    {
        public readonly RDMUID Uid;
        public readonly PortAddress PortAddress;
        public readonly IPv4Address IpAddress;
        public DateTime LastSeen { get; private set; }

        public ControllerRDMUID_Bag(in RDMUID uid, in PortAddress portAddress, in IPv4Address ipAddress)
        {
            Uid = uid;
            PortAddress = portAddress;
            IpAddress = ipAddress;
            Seen();
        }

        internal void Seen()
        {
            LastSeen = DateTime.UtcNow;
        }
        internal bool Timouted()
        {
            var now = DateTime.UtcNow.AddSeconds(-30);
            return LastSeen <= now;
        }
    }
}