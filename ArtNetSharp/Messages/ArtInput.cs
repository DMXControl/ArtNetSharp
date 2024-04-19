using System;

namespace ArtNetSharp
{
#pragma warning disable CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    public sealed class ArtInput : AbstractArtPacket
#pragma warning restore CS0659 // Typ überschreibt Object.Equals(object o), überschreibt jedoch nicht Object.GetHashCode()
    {
        public override sealed EOpCodes OpCode => EOpCodes.OpInput;
        protected override sealed ushort PacketMinLength => 20;
        /// <summary>
        /// The BindIndexdefines the bound node which
        /// originated this packet.In combination with Port and
        /// Source IP address, it uniquely identifiesthe sender.
        /// This must match the BindIndex field in ArtPollReply.
        /// This number represents the order of bound devices.
        /// A lower number means closer to root device. A value
        /// of 1 means root device.
        /// </summary>
        public readonly byte BindIndex;
        public readonly byte NumPortsHi;
        public readonly byte NumPortsLo;
        public readonly EArtInputCommand[] Input;

        private const byte maxNumPortsLo = 4;

        public ArtInput(in byte bindIndex,
                        EArtInputCommand[] input,
                        in byte numPortsLo = 1,
                        in byte numPortsHi = 0,
                        in ushort protocolVersion = Constants.PROTOCOL_VERSION) : base(protocolVersion)
        {
            if (numPortsLo > maxNumPortsLo)
                throw new ArgumentOutOfRangeException($"The limit of NumPortsLo is {maxNumPortsLo}");
            if (numPortsHi > 0)
                throw new ArgumentOutOfRangeException($"The limit of NumPortsHi is {0}");
            if (input.Length != numPortsLo)
                throw new ArgumentOutOfRangeException($"Input length and NumPortsLo should be identical");

            BindIndex = bindIndex;
            NumPortsHi = numPortsHi;
            NumPortsLo = numPortsLo;
            Input = input;
        }
        public ArtInput(in byte[] packet) : base(packet)
        {
            BindIndex = packet[13];
            NumPortsHi = packet[14];
            NumPortsLo = packet[15];
            Input = new EArtInputCommand[packet.Length - 16];
            for (int i = 0; i < Input.Length; i++)
                Input[i] = (EArtInputCommand)packet[16 + i];
        }
        protected sealed override void fillPacket(ref byte[] p)
        {
            //p[12] = 0 // Filler 1
            p[13] = BindIndex; // BindIndex
            p[14] = NumPortsHi; // NumPortsHi
            p[15] = NumPortsLo; // NumPortsLo
            for (int i = 0; i < Input.Length; i++)
                p[16] = (byte)Input[i];
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && obj is ArtInput data
                && BindIndex == data.BindIndex
                && NumPortsHi == data.NumPortsHi
                && NumPortsLo == data.NumPortsLo;
        }

        public static implicit operator byte[](ArtInput artInput)
        {
            return artInput.GetPacket();
        }
    }
}