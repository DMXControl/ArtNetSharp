using System.Threading.Tasks;

namespace ArtNetSharp.Communication
{
    public class NodeInstance : AbstractInstance
    {
        public NodeInstance(ArtNet _artnet, bool supportRDM = false) : base(_artnet)
        {
            this.supportRDM = supportRDM;
        }

        public sealed override EStCodes EstCodes => EStCodes.StNode;
        protected sealed override bool SendArtPollBroadcast => false;
        protected sealed override bool SendArtPollTargeted => true;
        private bool supportRDM = false;
        protected override bool SupportRDM => supportRDM;

        protected override void OnPacketReceived(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp)
        {
            //switch (packet)
            //{
            //}
        }
        public async Task PerformRDMDiscovery(PortAddress? portAddress = null, bool flush = false)
        {
            await base.PerformRDMDiscovery(portAddress, flush, true); // As per spec Broadcast 1.4dh 19/7/2023 - 82 -
        }
    }
}
