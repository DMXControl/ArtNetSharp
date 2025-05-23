﻿using ArtNetSharp;

namespace ArtNetTests.Binary_Tests.Showtec
{
    internal class Pknight_CR011R : AbstractArtPollReplyBinaryTestSubject
    {
        private static readonly byte[] DATA = [
            0x41, 0x72, 0x74, 0x2d, 0x4e, 0x65, 0x74, 0x00,
            0x00, 0x21, 0x0a, 0xc9, 0x06, 0x64, 0x36, 0x19,
            0x00, 0x0e, 0x00, 0x00, 0x00, 0x22, 0x00, 0x31,
            0x7a, 0x70, 0x43, 0x52, 0x30, 0x31, 0x31, 0x52,
            0x5f, 0x30, 0x30, 0x31, 0x00, 0x00, 0x5c, 0x01,
            0x00, 0x20, 0x70, 0xb5, 0x43, 0x52, 0x30, 0x31,
            0x31, 0x52, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x30, 0x30, 0x30, 0x31,
            0x20, 0x5b, 0x30, 0x32, 0x32, 0x30, 0x5d, 0x20,
            0x41, 0x72, 0x74, 0x4e, 0x65, 0x74, 0x00, 0x31,
            0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31,
            0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31,
            0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31,
            0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31,
            0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31,
            0x31, 0x31, 0x31, 0x31, 0x00, 0x01, 0x80, 0x31,
            0x31, 0x31, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00,
            0x00, 0x00, 0x00, 0x31, 0x31, 0x31, 0x00, 0x31,
            0x31, 0x31, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x02, 0x4d, 0x48, 0xc9, 0x06, 0x64, 0x0a,
            0xc9, 0x06, 0x64, 0x01, 0x08, 0x31 ];

        private static readonly PortTestSubject[] PORTS =
        [
            new PortTestSubject(EPortType.OutputFromArtNet,(PortAddress)0,(PortAddress)0)
        ];
        public Pknight_CR011R() : base(
            "Pknight CR011R",
            DATA,
            0,
            "CR011R_001",
            "CR011R",
            new MACAddress("02:4d:48:c9:06:64"),
            new IPv4Address("10.201.6.100"),
            new IPv4Address("10.201.6.100"),
            0x0022,
            0x707a,
            EStCodes.StNode,
            PORTS,
            false, //Compleatly Buggy. Provided by Mario from Berlin
            0,
            14)
        {
        }
    }
}
