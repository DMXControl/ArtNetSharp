﻿using ArtNetSharp;
using RDMSharp;

namespace ArtNetTests.Binary_Tests.Showtec
{
    internal class Showtec_Net2 : AbstractArtPollReplyBinaryTestSubject
    {
        private static readonly byte[] DATA = [
            0x41, 0x72, 0x74, 0x2d, 0x4e, 0x65, 0x74, 0x00,
            0x00, 0x21, 0x02, 0x01, 0x01, 0x01, 0x36, 0x19,
            0x01, 0x02, 0x01, 0x00, 0xff, 0xff, 0x00, 0xe2,
            0xff, 0xff, 0x50, 0x6f, 0x72, 0x74, 0x20, 0x31,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x4e, 0x65, 0x74, 0x57,
            0x6f, 0x72, 0x6b, 0x20, 0x4e, 0x6f, 0x64, 0x65,
            0x20, 0x32, 0x28, 0x34, 0x32, 0x3a, 0x34, 0x43,
            0x3a, 0x34, 0x42, 0x3a, 0x36, 0x34, 0x3a, 0x33,
            0x46, 0x3a, 0x38, 0x36, 0x29, 0x00, 0x00, 0x00,
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
            0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x03, 0x00,
            0x00, 0x00, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x42, 0x4c, 0x4b, 0x64, 0x3f, 0x86, 0x02,
            0x01, 0x01, 0x01, 0x01, 0xdd, 0x40, 0x00, 0x00,
            0x00, 0x2c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ];

        private static readonly PortTestSubject[] PORTS =
        [
            new PortTestSubject(EPortType.OutputFromArtNet,(Universe)3,(Universe)3)
        ];
        public Showtec_Net2() : base(
            "Showtec Net2 (FW 1.2)",
            DATA,
            1,
            "Port 1",
            "NetWork Node 2(42:4C:4B:64:3F:86)",
            new MACAddress("42:4C:4B:64:3F:86"),
            new IPv4Address("2.1.1.1"),
            new IPv4Address("2.1.1.1"),
            0xffff,
            0xffff,
            EStCodes.StNode,
            PORTS,
            true,
            1,
            2)
        {
        }
    }
}
