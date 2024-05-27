using org.dmxc.wkdt.Light.RDM;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ArtNetTests")]
namespace ArtNetSharp.Communication
{
    public sealed class ControllerRDMUID_Bag : IEquatable<ControllerRDMUID_Bag>
    {
        public readonly UID Uid;
        public readonly PortAddress PortAddress;
        public readonly IPv4Address IpAddress;
        public DateTime LastSeen { get; private set; }

        public ControllerRDMUID_Bag(in UID uid, in PortAddress portAddress, in IPv4Address ipAddress)
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

        public override bool Equals(object obj)
        {
            return Equals(obj as ControllerRDMUID_Bag);
        }

        public bool Equals(ControllerRDMUID_Bag other)
        {
            return other is not null &&
                   Uid.Equals(other.Uid) &&
                   PortAddress.Equals(other.PortAddress) &&
                   IpAddress.Equals(other.IpAddress);
        }

        public override int GetHashCode()
        {
            return Uid.GetHashCode() + PortAddress.GetHashCode() + IpAddress.GetHashCode();
        }

        public static bool operator ==(ControllerRDMUID_Bag left, ControllerRDMUID_Bag right)
        {
            return EqualityComparer<ControllerRDMUID_Bag>.Default.Equals(left, right);
        }

        public static bool operator !=(ControllerRDMUID_Bag left, ControllerRDMUID_Bag right)
        {
            return !(left == right);
        }
    }
}