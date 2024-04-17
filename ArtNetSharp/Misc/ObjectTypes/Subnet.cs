using System;

namespace ArtNetSharp
{
    public readonly struct Subnet : IEquatable<Subnet>, IComparable<Subnet>
    {
        public static readonly Subnet Default = new Subnet();
        public readonly byte Value;

        public Subnet(in byte value)
        {
            if ((byte)(value & 0x0f) != value)
                throw new ArgumentException($"Value (0x{value:x}) out of range! A valid value is between 0x00 and 0x0f.");
            Value = value;
        }

        public static implicit operator byte(Subnet subnet)
        {
            return subnet.Value;
        }
        public static implicit operator Subnet(byte b)
        {
            return new Subnet(b);
        }

        public override bool Equals(object obj)
        {
            return obj is Subnet other &&
                   Equals(other.Value);
        }

        public override int GetHashCode()
        {
            int hashCode = -971048872;
            hashCode = hashCode * -1521134295 + Value.GetHashCode();
            return hashCode;
        }
        public override string ToString()
        {
            return $"Subnet: {Value}(0x{Value:x1})";
        }

        public static bool operator ==(Subnet a, Subnet b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Subnet a, Subnet b)
        {
            return !a.Equals(b);
        }

        public bool Equals(Subnet other)
        {
            return Value == other.Value;
        }

        public int CompareTo(Subnet other)
        {
            return Value.CompareTo(other.Value);
        }
    }
}