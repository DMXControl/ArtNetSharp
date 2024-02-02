﻿using RDMSharp;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtNetSharp.Communication
{
    public class ControllerInstance : AbstractInstance
    {
        public sealed override EStCodes EstCodes => EStCodes.StController;
        protected sealed override bool SendArtPollBroadcast => false;
        protected sealed override bool SendArtPollTargeted => true;
        protected sealed override bool SendArtData => true;
        protected sealed override bool SupportRDM => true;

        protected override async void OnPacketReceived(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp)
        {
            switch (packet)
            {

                case ArtDataReply artDataReply:
                    await processArtDataReply(artDataReply, sourceIp);
                    break;
            }
        }

        private async Task processArtDataReply(ArtDataReply artDataReply, IPv4Address sourceIp)
        {
            List<Task> tasks = new List<Task>();
            foreach (var client in RemoteClients.Where(rc => rc.IpAddress.Equals(sourceIp)))
                tasks.Add(client.processArtDataReply(artDataReply));

            await Task.WhenAll(tasks);
        }

        public async Task PerformRDMDiscovery(PortAddress? portAddress = null, bool flush = false)
        {
            await base.PerformRDMDiscovery(portAddress, flush);
        }
    }
}
