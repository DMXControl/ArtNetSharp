using ArtNetSharp.Messages.Interfaces;

namespace ArtNetSharp.Communication
{
    internal interface IInstance : IDisposableExtended
    {
        string Name { get; set; }
        string ShortName { get; set; }
        EStCodes EstCodes { get; }

        void PacketReceived(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp);
    }
}
