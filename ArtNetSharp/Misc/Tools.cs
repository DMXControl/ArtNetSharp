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
        public static bool TryDeserializePacket(byte[] data,out AbstractArtPacketCore packet)
        {
            packet = null;
            try
            {
                if (data == null)
                    return false;
                if (data.Length < 10)
                    return false;

                if (data[0] == 'A'
                    && data[1] == 'r'
                    && data[2] == 't'
                    && data[3] == '-'
                    && data[4] == 'N'
                    && data[5] == 'e'
                    && data[6] == 't'
                    && data[7] == 0x00)
                {
                    EOpCodes opCode = (EOpCodes)(data[9] << 8 | data[8]);
                    switch (opCode)
                    {
                        case EOpCodes.OpPoll:
                            packet = new ArtPoll(data);
                            break;
                        case EOpCodes.OpPollReply:
                            packet = new ArtPollReply(data);
                            break;

                        case EOpCodes.OpInput:
                            packet = new ArtInput(data);
                            break;

                        case EOpCodes.OpSync:
                            packet = new ArtSync(data);
                            break;
                        case EOpCodes.OpOutput:
                            packet = new ArtDMX(data);
                            break;

                        case EOpCodes.OpNzs:
                            if (ArtVlc.IsArtVlc(data))
                            {
                                packet = new ArtVlc(data);
                                break;
                            }
                            packet = new ArtNzs(data);
                            break;

                        case EOpCodes.OpRdm:
                            packet = new ArtRDM(data);
                            break;
                        case EOpCodes.OpRdmSub:
                            packet = new ArtRDMSub(data);
                            break;
                        case EOpCodes.OpTodControl:
                            packet = new ArtTodControl(data);
                            break;
                        case EOpCodes.OpTodRequest:
                            packet = new ArtTodRequest(data);
                            break;
                        case EOpCodes.OpTodData:
                            packet = new ArtTodData(data);
                            break;


                        case EOpCodes.OpIpProg:
                            packet = new ArtIpProg(data);
                            break;
                        case EOpCodes.OpIpProgReply:
                            packet = new ArtIpProgReply(data);
                            break;

                        case EOpCodes.OpTimeCode:
                            packet = new ArtTimeCode(data);
                            break;
                        case EOpCodes.OpTimeSync:
                            packet = new ArtTimeSync(data);
                            break;

                        case EOpCodes.OpAddress:
                            packet = new ArtAddress(data);
                            break;

                        case EOpCodes.OpDataRequest:
                            packet = new ArtData(data);
                            break;
                        case EOpCodes.OpDataReply:
                            packet = new ArtDataReply(data);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                return false;
            }
            return packet != null;
        }
        public static bool BitsMatch(byte value, byte mask)
        {
            byte result = (byte)(value & mask);
            return result == mask;
        }
    }
}