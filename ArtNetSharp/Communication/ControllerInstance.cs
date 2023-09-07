using RDMSharp;
using System.Threading.Tasks;

namespace ArtNetSharp.Communication
{
    public class ControllerInstance : AbstractInstance
    {
        public sealed override EStCodes EstCodes => EStCodes.StController;
        protected sealed override bool SendArtPoll => true;
        protected sealed override bool SupportRDM => true;

        protected override void OnPacketReceived(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp)
        {
        }
        public async Task PerformRDMDiscovery(PortAddress? portAddress = null, bool flush = false)
        {
            await base.PerformRDMDiscovery(portAddress,flush);
        }
    }
}
