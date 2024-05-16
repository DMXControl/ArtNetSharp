﻿using ArtNetSharp;
using RDMSharp;

namespace ArtNetTests.Binary_Tests.Luminex
{
    internal class Elation_Obisdian_RDM10_2_9_4 : AbstractArtPollReplyBinaryTestSubject
    {
        private static readonly byte[] DATA= [
            0x41, 0x72, 0x74, 0x2d, 0x4e, 0x65, 0x74, 0x00,
            0x00, 0x21, 0x02, 0x5e, 0x4a, 0x67, 0x36, 0x19,
            0x02, 0x5c, 0x00, 0x00, 0x2a, 0x1e, 0x00, 0xe2,
            0xa6, 0x22, 0x50, 0x6f, 0x72, 0x74, 0x20, 0x31,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x4e, 0x45, 0x54, 0x52,
            0x4f, 0x4e, 0x20, 0x52, 0x44, 0x4d, 0x31, 0x30,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x80, 0x00,
            0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x80, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x42, 0x4c, 0x4a, 0x16, 0x4a, 0x67, 0x02,
            0x5e, 0x4a, 0x67, 0x01, 0xdd, 0xc0, 0x00, 0x00,
            0x00, 0x3c, 0x22, 0xa6, 0x06, 0xd3, 0x01, 0x67,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ];

        private static readonly PortTestSubject[] PORTS =
        [
            new PortTestSubject(EPortType.OutputFromArtNet,(Universe)0,(Universe)0)
        ];
        public Elation_Obisdian_RDM10_2_9_4() : base(
            "Elation Obsidian RDM10 (FW 2.9.4)",
            DATA,
            0,
            "Port 1",
            "NETRON RDM10",
            new MACAddress("42:4c:4a:16:4a:67"),
            new IPv4Address("2.94.74.103"),
            new IPv4Address("2.94.74.103"),
            0x2a1e,
            0x22a6,
            EStCodes.StNode,
            PORTS,
            true,
            majorVersion:2,
            minorVersion:94)
        {
        }
    }
}
