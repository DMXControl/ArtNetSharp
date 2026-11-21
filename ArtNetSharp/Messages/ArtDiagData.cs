using System;
using System.Linq;
using System.Text;

namespace ArtNetSharp
{
    /// <summary>
    /// ArtDiagData: diagnostics text a node or controller sends for display (e.g. in DMX-Workshop).
    /// Controllers request it via ArtPoll (Flags bit 2, bit 3 unicast/broadcast, DiagPriority = minimum priority).
    /// </summary>
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public sealed class ArtDiagData : AbstractArtPacket
#pragma warning restore CS0659
    {
        /// <summary>Spec: max 512 bytes including the null terminator.</summary>
        public const int MaxTextLength = 511;

        public override sealed EOpCodes OpCode => EOpCodes.OpDiagData;
        protected override sealed ushort PacketMinLength => 18;
        protected override sealed ushort PacketMaxLength => (ushort)(PacketMinLength + MaxTextLength + 1);
        protected override sealed ushort PacketBuildLength => (ushort)(PacketMinLength + textBytes.Length + 1);

        /// <summary>Priority of this message (DpLow ... DpVolatile).</summary>
        public readonly EPriorityCode Priority;
        /// <summary>Logical DMX port the message relates to; 0 for general messages. Informational only.</summary>
        public readonly byte LogicalPort;
        /// <summary>The diagnostics text (ASCII).</summary>
        public readonly string Text;

        private readonly byte[] textBytes;

        public ArtDiagData(in string text,
                           in EPriorityCode priority = EPriorityCode.DpLow,
                           in byte logicalPort = 0,
                           in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(protocolVersion)
        {
            Text = text ?? string.Empty;
            if (Text.Length > MaxTextLength)
                Text = Text.Substring(0, MaxTextLength);
            textBytes = Encoding.ASCII.GetBytes(Text);
            Priority = priority;
            LogicalPort = logicalPort;
        }

        public ArtDiagData(in byte[] packet) : base(packet)
        {
            // 12 Filler1, 13 DiagPriority, 14 LogicalPort, 15 Filler3, 16-17 Length (Hi, Lo), 18.. Data
            Priority = (EPriorityCode)packet[13];
            LogicalPort = packet[14];
            int length = Math.Min(packet[16] << 8 | packet[17], packet.Length - 18);
            Text = Encoding.ASCII.GetString(packet, 18, Math.Max(0, length)).Split('\0')[0];
            textBytes = Encoding.ASCII.GetBytes(Text);
        }

        protected sealed override void fillPacket(ref byte[] p)
        {
            p[13] = (byte)Priority;
            p[14] = LogicalPort;
            ushort length = (ushort)(textBytes.Length + 1); // including the null terminator
            p[16] = (byte)(length >> 8);
            p[17] = (byte)(length & 0xff);
            Array.Copy(textBytes, 0, p, 18, textBytes.Length);
            // p[18 + textBytes.Length] = 0: null terminator (array is zero-initialised)
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtDiagData other
                && Priority == other.Priority
                && LogicalPort == other.LogicalPort
                && Text == other.Text;
        }

        public override string ToString() => $"{nameof(ArtDiagData)}: [{Priority}] Port {LogicalPort}: {Text}";
    }
}
