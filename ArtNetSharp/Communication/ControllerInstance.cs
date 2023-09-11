using RDMSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ArtNetSharp.Communication
{
    public class ControllerInstance : AbstractInstance
    {
        public sealed override EStCodes EstCodes => EStCodes.StController;
        protected sealed override bool SendArtPoll => true;
        protected sealed override bool SendArtData => true;
        protected sealed override bool SupportRDM => true;

        protected override void OnPacketReceived(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp)
        {
            switch(packet)
            {

                case ArtDataReply artDataReply:
                    processArtDataReply(artDataReply, sourceIp);
                    break;
            }
        }

        private void processArtDataReply(ArtDataReply artDataReply, IPv4Address sourceIp)
        {
            foreach (var client in RemoteClients.Where(rc => rc.IpAddress.Equals(sourceIp)))
                client.processArtDataReply(artDataReply);
        }

        public async Task PerformRDMDiscovery(PortAddress? portAddress = null, bool flush = false)
        {
            await base.PerformRDMDiscovery(portAddress,flush);
        }
    }
}
