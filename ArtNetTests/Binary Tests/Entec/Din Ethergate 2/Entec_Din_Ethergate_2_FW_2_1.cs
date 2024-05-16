﻿using ArtNetSharp;
using RDMSharp;

namespace ArtNetTests.Binary_Tests.Luminex
{
    internal class Entec_Din_Ethergate_2_FW_2_1 : AbstractArtPollReplyBinaryTestSubject
    {
        private static readonly byte[] DATA= [
            0x41, 0x72, 0x74, 0x2d, 0x4e, 0x65, 0x74, 0x00,
            0x00, 0x21, 0x02, 0x00, 0x00, 0x55, 0x36, 0x19,
            0x02, 0x01, 0x00, 0x00, 0x01, 0x90, 0x00, 0x02,
            0x4e, 0x45, 0x45, 0x74, 0x68, 0x65, 0x72, 0x67,
            0x61, 0x74, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x44, 0x49, 0x4e, 0x20,
            0x45, 0x74, 0x68, 0x65, 0x72, 0x67, 0x61, 0x74,
            0x65, 0x20, 0x32, 0x00, 0x00, 0x00, 0x00, 0x00,
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
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x82, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0xd0, 0x9f, 0xd9, 0x90, 0x45, 0x16, 0x02,
            0x00, 0x00, 0x55, 0x01, 0x0d, 0x00, 0x00, 0x80,
            0x80, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ];

        private static readonly PortTestSubject[] PORTS =
        [
            new PortTestSubject(EPortType.OutputFromArtNet,(Universe)0,(Universe)0)
        ];
        public Entec_Din_Ethergate_2_FW_2_1() : base(
            "ENTEC DIN Ethergate 2 (FW 2.1)",
            DATA,
            0,
            "Ethergate",
            "DIN Ethergate 2",
            new MACAddress("d0:9f:d9:90:45:16"),
            new IPv4Address("2.0.0.85"),
            new IPv4Address("2.0.0.85"),
            0x0190,
            0x454e,
            EStCodes.StNode,
            PORTS,
            false, //[215] GoodOutputB RDM Flag on port 3 & 4 set, but should be 0
            majorVersion: 2,
            minorVersion: 1)
        {
        }
    }
}
