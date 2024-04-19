using System;

namespace ArtNetSharp
{
    public readonly struct Address : IEquatable<Address>, IComparable<Address>
    {
        public readonly Subnet Subnet;
        public readonly Universe Universe;
        public readonly byte Combined;

        public Address(in Subnet subnet, in Universe universe)
        {
            Subnet = subnet;
            Universe = universe;
            Combined = (byte)((Subnet & 0xf) << 4 | (Universe & 0xf));
        }
        public Address(in byte combined)
        {
            Subnet = (Subnet)((combined >> 4) & 0xf);
            Universe = (Universe)(combined & 0xf);
            Combined = combined;
        }

        public static implicit operator byte(Address address)
        {
            return address.Combined;
        }
        public static implicit operator Address(byte b)
        {
            return new Address(b);
        }

        public override bool Equals(object obj)
        {
            return obj is Address other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            int hashCode = -971048872;
            hashCode = hashCode * -1521134295 + Combined.GetHashCode();
            return hashCode;
        }
        public override string ToString()
        {
            return $"{Combined}(0x{Combined:x2}) / Subnet: {Subnet}(0x{Subnet:x1}), Universe: {Universe}(0x{Universe:x1})";
        }

        public static bool operator ==(Address a, Address b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Address a, Address b)
        {
            return !a.Equals(b);
        }

        public bool Equals(Address other)
        {
            return Combined == other.Combined;
        }

        public int CompareTo(Address other)
        {
            return Combined.CompareTo(other.Combined);
        }
    }
}