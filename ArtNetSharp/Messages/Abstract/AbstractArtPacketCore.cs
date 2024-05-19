using ArtNetSharp.Messages.Interfaces;
using System;

namespace ArtNetSharp
{
    public abstract class AbstractArtPacketCore : IDisposableExtended
    {
        public abstract EOpCodes OpCode { get; }
        protected abstract ushort PacketMinLength { get; }
        protected virtual ushort PacketMaxLength { get { return PacketMinLength; } }
        protected virtual ushort PacketBuildLength { get { return PacketMaxLength; } }

        protected bool PacketLengthIsMinimum { get { return PacketMinLength != PacketMaxLength; } }

        protected bool IsDisposing { get; private set; }
        bool IDisposableExtended.IsDisposing { get => IsDisposing; }
        protected bool IsDisposed { get; private set; }
        bool IDisposableExtended.IsDisposed { get => IsDisposed; }

        protected int? HashCode { get; private set; }
        private byte[] packetData { get; set; }

        protected AbstractArtPacketCore()
        {
        }
        public AbstractArtPacketCore(in byte[] packet) : this()
        {
            EOpCodes opCode = (EOpCodes)(ushort)(packet[9] << 8 | packet[8]);
            if (opCode != OpCode)
                throw new ArgumentException($"Wrong OpCode ({opCode}), should be {OpCode}");

            if (PacketLengthIsMinimum)
            {
                if (packet.Length < PacketMinLength)
                    throw new ArgumentOutOfRangeException($"This Packet({packet.Length}) should be at least {PacketMinLength} bytes or larger");
            }
            else if (packet.Length != PacketMinLength)
                throw new ArgumentOutOfRangeException($"This Packet({packet.Length}) should be {PacketMinLength} bytes");
        }
        ~AbstractArtPacketCore()
        {
            ((IDisposable)this).Dispose();
        }

        public byte[] GetPacket()
        {
            if (this.IsDisposing || this.IsDisposed)
                throw new ObjectDisposedException(this.GetType().FullName);

            if (packetData != null)
                return packetData;

            byte[] p = new byte[(int)PacketBuildLength];

            Tools.FillDefaultPacket(OpCode, ref p);
            fillPacketCore(ref p);
            packetData = p;
            return p;
        }

        protected abstract void fillPacketCore(ref byte[] packet);

        public static implicit operator byte[](AbstractArtPacketCore abstractArtPacketCore)
        {
            return abstractArtPacketCore.GetPacket();
        }
        public static bool operator ==(AbstractArtPacketCore a, AbstractArtPacketCore b)
        {
            if (a is null && b is null)
                return true;

            return a?.Equals(b) ?? b.Equals(a);
        }

        public static bool operator !=(AbstractArtPacketCore a, AbstractArtPacketCore b)
        {
            if (a is null && b is null)
                return false;

            return !(a?.Equals(b) ?? b.Equals(a));
        }
        public override bool Equals(object obj)
        {
            return obj is AbstractArtPacketCore other
                && OpCode == other.OpCode;
        }

        public sealed override int GetHashCode()
        {
            if (HashCode.HasValue)
                return HashCode.Value;

            unchecked
            {
                var result = 0;
                foreach (byte b in GetPacket())
                    result = (result * 31) ^ b;
                HashCode = result;
            }
            return HashCode.Value;
        }

        void IDisposable.Dispose()
        {
            if (this.IsDisposing || this.IsDisposed)
                return;

            IsDisposing = true;
            try
            {
                this.Dispose();
            }
            catch { }
            IsDisposed = true;
            IsDisposing = false;
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose()
        {

        }
    }
}