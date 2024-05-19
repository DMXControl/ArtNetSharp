﻿using ArtNetSharp;
using RDMSharp;

namespace ArtNetTests.Binary_Tests.SK_Software
{
    internal class SK_Software_AvrNode : AbstractArtPollReplyBinaryTestSubject
    {
        private static readonly byte[] DATA = [
            0x41, 0x72, 0x74, 0x2d, 0x4e, 0x65, 0x74, 0x00,
            0x00, 0x21, 0xc0, 0xa8, 0x01, 0x5a, 0x36, 0x19,
            0x01, 0x00, 0x00, 0x00, 0x08, 0xb1, 0x00, 0x00,
            0x4b, 0x53, 0x41, 0x76, 0x72, 0x41, 0x72, 0x74,
            0x4e, 0x6f, 0x64, 0x65, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x41, 0x56, 0x52, 0x20,
            0x62, 0x61, 0x73, 0x65, 0x64, 0x20, 0x41, 0x72,
            0x74, 0x2d, 0x4e, 0x65, 0x74, 0x20, 0x6e, 0x6f,
            0x64, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x23, 0x30, 0x30, 0x30,
            0x31, 0x20, 0x5b, 0x30, 0x30, 0x30, 0x30, 0x5d,
            0x20, 0x41, 0x76, 0x72, 0x41, 0x72, 0x74, 0x4e,
            0x6f, 0x64, 0x65, 0x20, 0x69, 0x73, 0x20, 0x72,
            0x65, 0x61, 0x64, 0x79, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x80, 0x00,
            0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x02, 0x00,
            0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x22, 0xf9, 0x01, 0x60, 0x49, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ];

        private static readonly PortTestSubject[] PORTS =
        [
            new PortTestSubject(EPortType.OutputFromArtNet,(Address)0,(Address)1),
        ];
        public SK_Software_AvrNode() : base(
            "AVR based Art-Net node (FW 1.0)",
            DATA,
            0,
            "AvrArtNode",
            "AVR based Art-Net node",
            new MACAddress("00:22:f9:01:60:49"),
            new IPv4Address("192.168.1.90"),
            new IPv4Address("0.0.0.0"),
            0x08b1,
            0x534b,
            EStCodes.StNode,
            PORTS,
            true,
            1,
            0,
            new NodeReport(ENodeReportCodes.RcPowerOk, "AvrArtNode is ready", 0))
        {
        }
    }
}
