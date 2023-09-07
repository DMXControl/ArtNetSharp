using RDMSharp;
using System;

namespace ArtNetSharp.Communication
{
    internal interface IInstance: IDisposable
    {
        string Name { get; set; }
        string ShortName { get; set; }
        EStCodes EstCodes { get; }

        void PacketReceived(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp);
    }
}
