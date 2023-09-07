using Microsoft.Extensions.Logging;
using RDMSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ArtNetSharp.Communication
{
    public class RemoteClientPort
    {
        private static ILogger Logger = ApplicationLogging.CreateLogger<RemoteClientPort>();
        public readonly IPv4Address IpAddress;
        public readonly byte BindIndex;
        public readonly byte PortIndex;
        public DateTime LastSeen { get; private set; }
        public ArtPollReply ArtPollReply { get; private set; }
        public PortAddress? OutputPortAddress { get; private set; }
        public PortAddress? InputPortAddress { get; private set; }
        public EPortType PortType { get; private set; }
        public EGoodOutput GoodOutput { get; private set; }
        public EGoodInput GoodInput { get; private set; }

        public bool IsRDMCapable { get; private set; }

        private ConcurrentDictionary<RDMUID, RDMUID_ReceivedBag> knownRDMUIDs = new ConcurrentDictionary<RDMUID, RDMUID_ReceivedBag>();
        public IReadOnlyCollection<RDMUID_ReceivedBag> KnownRDMUIDs;
        public event EventHandler<RDMUID_ReceivedBag> RDMUIDReceived;
        public event EventHandler<byte[]> RDMMessageReceived;


        private byte sequence = byte.MaxValue;
        internal byte Sequence
        {
            get
            {
                sequence++;
                return sequence;
            }
        }

        public RemoteClientPort(in ArtPollReply artPollReply, byte portIndex = 0)
        {
            IpAddress = artPollReply.OwnIp;
            BindIndex = artPollReply.BindIndex;
            PortIndex = portIndex;
            processArtPollReply(artPollReply);
            KnownRDMUIDs = knownRDMUIDs.Values.ToList().AsReadOnly();
        }

        public void processArtPollReply(ArtPollReply artPollReply)
        {
            if (!IpAddress.Equals(artPollReply.OwnIp))
                return;
            if (!BindIndex.Equals(artPollReply.BindIndex))
                return;
            if (PortIndex >= artPollReply.Ports)
                return;

            ArtPollReply = artPollReply;

            PortType = artPollReply.PortTypes[PortIndex];
            GoodOutput = artPollReply.GoodOutput[PortIndex];
            GoodOutput = artPollReply.GoodOutput[PortIndex];
            Universe output = artPollReply.OutputUniverses[PortIndex];
            Universe input = artPollReply.InputUniverses[PortIndex];
            IsRDMCapable = artPollReply.Status.HasFlag(ENodeStatus.RDM_Supported) && !GoodOutput.HasFlag(EGoodOutput.RDMisDisabled);

            if (PortType.HasFlag(EPortType.OutputFromArtNet))
                OutputPortAddress = new PortAddress(artPollReply.Net, artPollReply.Subnet, output);
            else
                OutputPortAddress = null;

            if (PortType.HasFlag(EPortType.InputToArtNet))
                InputPortAddress = new PortAddress(artPollReply.Net, artPollReply.Subnet, input);
            else
                InputPortAddress = null;

            LastSeen = DateTime.UtcNow;
        }
        internal void AddRdmUIDs(params RDMUID[] rdmuids)
        {
            if (rdmuids.Length == 0)
                return;

            foreach (RDMUID rdmuid in rdmuids)
            {
                RDMUID_ReceivedBag bag;
                if (knownRDMUIDs.TryGetValue(rdmuid, out bag))
                    bag.Seen();
                else
                {
                    bag= new RDMUID_ReceivedBag(rdmuid);
                    if (knownRDMUIDs.TryAdd(rdmuid, bag))
                    {
                        RDMUIDReceived?.Invoke(this, bag);
                        Logger.LogTrace($"{IpAddress}#{BindIndex} Cached UID: {bag.Uid}");
                    }
                }
            }
            KnownRDMUIDs = knownRDMUIDs.Values.ToList().AsReadOnly();
        }
        public void RemoveOutdatedRdmUIDs()
        {
            var outdated = knownRDMUIDs.Where(uid => uid.Value.Timouted()).ToList();
            bool removed = false;
            foreach (var remove in outdated)
                removed |= knownRDMUIDs.TryRemove(remove.Key, out _);
            if (removed)
                KnownRDMUIDs = knownRDMUIDs.Values.ToList().AsReadOnly();
        }
        public RDMUID[] GetReceivedRDMUIDs()
        {
            return KnownRDMUIDs.Where(k => !k.Timouted()).Select(k => k.Uid).ToArray();
        }

        internal void ProcessArtRDM(ArtRDM artRDM)
        {
            if (!KnownRDMUIDs.Any(k => k.Uid.Equals(artRDM.Source)))
                return;

            AddRdmUIDs(artRDM.Source);
            RDMMessageReceived?.Invoke(this, artRDM.Data);
        }

        public override string ToString()
        {
            return $"{nameof(RemoteClientPort)}: {IpAddress}#{BindIndex}";
        }
    }
}