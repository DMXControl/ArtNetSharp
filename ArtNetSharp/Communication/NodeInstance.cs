using RDMSharp;
using System;
using System.Threading.Tasks;

namespace ArtNetSharp.Communication
{
    public class NodeInstance : AbstractInstance
    {
        public sealed override EStCodes EstCodes => EStCodes.StNode;
        protected sealed override bool SendArtPoll => false;

        protected virtual string UrlProduct { get; }
        protected virtual string UrlUserGuid { get; }
        protected virtual string UrlSupport { get; }
        protected virtual string UrlPersonalityUDR { get; }
        protected virtual string UrlPersonalityGDTF { get; }

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

                case ArtData artData:
                    _ = processArtData(artData, sourceIp);
                    break;
            }
        }
        public async Task PerformRDMDiscovery(PortAddress? portAddress = null, bool flush = false)
        {
            await base.PerformRDMDiscovery(portAddress, flush, true); // As per spec Broadcast 1.4dh 19/7/2023 - 82 -
        }

        protected async Task processArtData(ArtData artData, IPv4Address source)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated || !this.SendArtData)
                return;

            try
            {
                if (artData.Request == EDataRequest.Poll)
                {
                    await TrySendPacket(new ArtDataReply(OEMProductCode, ESTAManufacturerCode, data: null), source);
                    return;
                }
                ArtDataReply packet = null;
                string str = null;
                switch (artData.Request)
                {
                    case EDataRequest.UrlProduct: str = UrlProduct; break;
                    case EDataRequest.UrlUserGuide: str = UrlUserGuid; break;
                    case EDataRequest.UrlSupport: str = UrlSupport; break;
                    case EDataRequest.UrlPersUdr: str = UrlPersonalityUDR; break;
                    case EDataRequest.UrlPersGdtf: str = UrlPersonalityGDTF; break;
                }
                if (!string.IsNullOrWhiteSpace(str))
                    packet = new ArtDataReply(OEMProductCode, ESTAManufacturerCode, artData.Request, str);

                if (packet == null)
                    packet = buildArtDataReply(artData);

                if (packet != null)
                    await TrySendPacket(packet, source);
            }
            catch (Exception ex) { Logger.LogError(ex); }
        }

        protected virtual ArtDataReply buildArtDataReply(ArtData artData)
        {

            return new ArtDataReply(OEMProductCode, ESTAManufacturerCode, artData.Request, data: null);
        }
    }
}
