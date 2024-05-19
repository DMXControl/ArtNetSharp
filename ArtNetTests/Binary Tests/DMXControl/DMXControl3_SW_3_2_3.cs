﻿using ArtNetSharp;
using RDMSharp;

namespace ArtNetTests.Binary_Tests.DMXControl
{
    internal class DMXControl3_SW_3_2_3 : AbstractArtPollReplyBinaryTestSubject
    {
        private static readonly byte[] DATA = [
            0x41, 0x72, 0x74, 0x2d, 0x4e, 0x65, 0x74, 0x00,
            0x00, 0x21, 0x0a, 0x63, 0x0c, 0x59, 0x36, 0x19,
            0x00, 0x00, 0x00, 0x00, 0x08, 0xb0, 0x00, 0x00,
            0x00, 0x00, 0x44, 0x4d, 0x58, 0x43, 0x20, 0x33,
            0x20, 0x28, 0x4d, 0x61, 0x69, 0x6b, 0x5f, 0x58,
            0x00, 0x00, 0x00, 0x00, 0x44, 0x4d, 0x58, 0x43,
            0x6f, 0x6e, 0x74, 0x72, 0x6f, 0x6c, 0x20, 0x33,
            0x20, 0x28, 0x4d, 0x61, 0x69, 0x6b, 0x5f, 0x58,
            0x4d, 0x47, 0x29, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x30, 0x30, 0x30, 0x31,
            0x20, 0x5b, 0x30, 0x30, 0x30, 0x30, 0x5d, 0x20,
            0x6c, 0x69, 0x62, 0x61, 0x72, 0x74, 0x6e, 0x65,
            0x74, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0xc0, 0xc0,
            0xc0, 0xc0, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05,
            0x06, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0xa4, 0xb1, 0xc1, 0xc4, 0x90, 0x0a, 0x0a,
            0x63, 0x0c, 0x59, 0x01, 0x08, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ];

        private static readonly PortTestSubject[] PORTS =
        [
            new PortTestSubject(EPortType.InputToArtNet | EPortType.OutputFromArtNet,(Universe)4,(Universe)0),
            new PortTestSubject(EPortType.InputToArtNet | EPortType.OutputFromArtNet,(Universe)5,(Universe)1),
            new PortTestSubject(EPortType.InputToArtNet | EPortType.OutputFromArtNet,(Universe)6,(Universe)2),
            new PortTestSubject(EPortType.InputToArtNet | EPortType.OutputFromArtNet,(Universe)7,(Universe)3)
        ];
        public DMXControl3_SW_3_2_3() : base(
            "DMXControl 3 (3.2.3)",
            DATA,
            0,
            "DMXC 3 (Maik_X",
            "DMXControl 3 (Maik_XMG)",
            new MACAddress("a4:b1:c1:c4:90:0a"),
            new IPv4Address("10.99.12.89"),
            new IPv4Address("10.99.12.89"),
            0x08b0,
            0x0000,
            EStCodes.StController,
            PORTS,
            false, //Bug in libartnet NodeReport is not starting with '#'
            0,
            0)
        {
        }
    }
}
