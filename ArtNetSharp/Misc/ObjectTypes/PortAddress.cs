using System;

namespace ArtNetSharp
{
    public readonly struct PortAddress : IEquatable<PortAddress>, IComparable<PortAddress>
    {
        public readonly Net Net;
        public readonly Subnet Subnet;
        public readonly Universe Universe;
        public readonly Address Address;
        public readonly ushort Combined;

        public PortAddress(in Net net, in Subnet subnet, in Universe universe)
        {
            Net = net;
            Subnet = subnet;
            Universe = universe;
            Address = new Address(subnet, universe);
            Combined = (ushort)((Net << 8) + Address.Combined);
        }
        public PortAddress(in ushort combined)
        {
            if ((ushort)(combined & 0x7fff) != combined)
                throw new ArgumentException($"Value (0x{combined:x}) out of range! A valid value is between 0x0000 and 0x7fff.");
            Net = (Net)((combined >> 8) & 0x7f);
            Subnet = (Subnet)((combined >> 4) & 0xf);
            Universe = (Universe)(combined & 0xf);
            Address = new Address(Subnet, Universe);
            Combined = combined;
        }
        public PortAddress(in Subnet subnet, Universe universe) : this(0, subnet, universe)
        {

        }
        public PortAddress(in Address address) : this(0, address.Subnet, address.Universe)
        {

        }
        public PortAddress(in Net net, in Address address) : this(net, address.Subnet, address.Universe)
        {

        }

        public static implicit operator ushort(PortAddress address)
        {
            return address.Combined;
        }
        public static implicit operator PortAddress(ushort b)
        {
            return new PortAddress(b);
        }

        public override bool Equals(object obj)
        {
            return obj is PortAddress other &&
                   Equals(other.Combined);
        }

        public override int GetHashCode()
        {
            int hashCode = -971048872;
            hashCode = hashCode * -1521134295 + Combined.GetHashCode();
            return hashCode;
        }
        public override string ToString()
        {
            return $"{Combined}(0x{Combined:x4}) / {Net}, {Subnet}, {Universe}";
        }

        public static bool operator ==(PortAddress a, PortAddress b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(PortAddress a, PortAddress b)
        {
            return !a.Equals(b);
        }

        public bool Equals(PortAddress other)
        {
            return Combined == other.Combined;
        }

        public int CompareTo(PortAddress other)
        {
            return Combined.CompareTo(other.Combined);
        }
    }
}