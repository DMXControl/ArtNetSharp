using Microsoft.Extensions.Logging;
using System;
using static ArtNetSharp.ApplicationLogging;

namespace ArtNetSharp
{
    public static class Tools
    {
        internal static ILoggerFactory LoggerFactory = new LoggerFactory(new[] { new FileProvider() });
        private static ILogger Logger = ApplicationLogging.CreateLogger("Tools");
        public static void FillDefaultPacket(EOpCodes opCode, ref byte[] packet)
        {
            packet[0] = (byte)'A'; packet[1] = (byte)'r'; packet[2] = (byte)'t'; packet[3] = (byte)'-'; packet[4] = (byte)'N'; packet[5] = (byte)'e'; packet[6] = (byte)'t'; packet[7] = 0x0; // ID
            FromUShort((ushort)opCode, out packet[8], out packet[9]);
        }
        public static void FromUShort(ushort number, out byte low, out byte high)
        {
            high = (byte)(number >> 8);
            low = (byte)(number & 255);
        }
        public static AbstractArtPacketCore DeserializePacket(byte[] packet)
        {
            try
            {
                if (packet == null)
                    return null;
                if (packet.Length < 10)
                    return null;

                if (packet[0] == 'A'
                    && packet[1] == 'r'
                    && packet[2] == 't'
                    && packet[3] == '-'
                    && packet[4] == 'N'
                    && packet[5] == 'e'
                    && packet[6] == 't'
                    && packet[7] == 0x00)
                {
                    EOpCodes opCode = (EOpCodes)(packet[9] << 8 | packet[8]);
                    switch (opCode)
                    {
                        case EOpCodes.OpPoll:
                            return new ArtPoll(packet);
                        case EOpCodes.OpPollReply:
                            return new ArtPollReply(packet);

                        case EOpCodes.OpInput:
                            return new ArtInput(packet);

                        case EOpCodes.OpSync:
                            return new ArtSync(packet);
                        case EOpCodes.OpOutput:
                            return new ArtDMX(packet);

                        case EOpCodes.OpNzs:
                            if (ArtVlc.IsArtVlc(packet))
                                return new ArtVlc(packet);
                            return new ArtNzs(packet);

                        case EOpCodes.OpRdm:
                            return new ArtRDM(packet);
                        case EOpCodes.OpRdmSub:
                            return new ArtRDMSub(packet);
                        case EOpCodes.OpTodControl:
                            return new ArtTodControl(packet);
                        case EOpCodes.OpTodRequest:
                            return new ArtTodRequest(packet);
                        case EOpCodes.OpTodData:
                            return new ArtTodData(packet);


                        case EOpCodes.OpIpProg:
                            return new ArtIpProg(packet);
                        case EOpCodes.OpIpProgReply:
                            return new ArtIpProgReply(packet);

                        case EOpCodes.OpTimeCode:
                            return new ArtTimeCode(packet);
                        case EOpCodes.OpTimeSync:
                            return new ArtTimeSync(packet);

                        case EOpCodes.OpAddress:
                            return new ArtAddress(packet);

                        case EOpCodes.OpDataRequest:
                            return new ArtData(packet);
                        case EOpCodes.OpDataReply:
                            return new ArtDataReply(packet);
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                return null;
            }
        }
    }
}