using RDMSharp;
using System.Threading.Tasks;

namespace ArtNetSharp.Communication
{
    public class NodeInstance : AbstractInstance
    {
        public sealed override EStCodes EstCodes => EStCodes.StNode;
        protected sealed override bool SendArtPoll => false;

        protected override void OnPacketReceived(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp)
        {
            switch (packet)
            {
                case ArtTodRequest artTodRequest:
                    _ = processArtTodRequest(artTodRequest, sourceIp);
                    break;
                case ArtTodControl artTodControl:
                    _ = processArtTodControl(artTodControl, sourceIp);
                    break;
            }
        }
        public async Task PerformRDMDiscovery(PortAddress? portAddress = null, bool flush = false)
        {
            await base.PerformRDMDiscovery(portAddress, flush, true); // As per spec Broadcast 1.4dh 19/7/2023 - 82 -
        }
    }
}
