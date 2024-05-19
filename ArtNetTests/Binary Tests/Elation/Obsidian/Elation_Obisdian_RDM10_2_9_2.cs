﻿using ArtNetSharp;
using RDMSharp;

namespace ArtNetTests.Binary_Tests.Luminex
{
    internal class Elation_Obisdian_RDM10_2_9_2 : AbstractArtPollReplyBinaryTestSubject
    {
        private static readonly byte[] DATA= [
            0x41, 0x72, 0x74, 0x2d, 0x4e, 0x65, 0x74, 0x00,
            0x00, 0x21, 0x02, 0x6a, 0x4e, 0x13, 0x36, 0x19,
            0x02, 0x5e, 0x00, 0x00, 0x2a, 0x1e, 0x00, 0xe2,
            0xa6, 0x22, 0x50, 0x6f, 0x72, 0x74, 0x20, 0x31,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x4e, 0x45, 0x54, 0x52,
            0x4f, 0x4e, 0x20, 0x52, 0x44, 0x4d, 0x31, 0x30,
            0x28, 0x34, 0x32, 0x3a, 0x34, 0x43, 0x3a, 0x41,
            0x32, 0x3a, 0x32, 0x32, 0x3a, 0x34, 0x45, 0x3a,
            0x31, 0x33, 0x29, 0x00, 0x00, 0x00, 0x00, 0x00,
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
            0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x40, 0x00,
            0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x42, 0x4c, 0xa2, 0x22, 0x4e, 0x13, 0x02,
            0x6a, 0x4e, 0x13, 0x01, 0xdd, 0xc0, 0x00, 0x00,
            0x00, 0x3c, 0x22, 0xa6, 0x06, 0xd3, 0x02, 0x28,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ];

        private static readonly PortTestSubject[] PORTS =
        [
            new PortTestSubject(EPortType.InputToArtNet,(Universe)0,(Universe)0)
        ];
        public Elation_Obisdian_RDM10_2_9_2() : base(
            "Elation Obsidian RDM10 (FW 2.9.2)",
            DATA,
            0,
            "Port 1",
            "NETRON RDM10(42:4C:A2:22:4E:13)",
            new MACAddress("42:4c:a2:22:4e:13"),
            new IPv4Address("2.106.78.19"),
            new IPv4Address("2.106.78.19"),
            0x2a1e,
            0x22a6,
            EStCodes.StNode,
            PORTS,
            true,
            majorVersion: 2,
            minorVersion: 92)
        {
        }
    }
}