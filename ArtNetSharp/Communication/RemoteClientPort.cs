using Microsoft.Extensions.Logging;
using RDMSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ArtNetSharp.Communication
{
    public sealed class RemoteClientPort : INotifyPropertyChanged
    {
        private static ILogger Logger = ApplicationLogging.CreateLogger<RemoteClientPort>();
        public readonly IPv4Address IpAddress;
        public readonly string ID;
        public readonly byte BindIndex;
        public readonly byte PortIndex;
        private DateTime lastSeen;
        public DateTime LastSeen
        {
            get
            {
                return lastSeen;
            }
            private set
            {
                if (lastSeen == value)
                    return;
                lastSeen = value;
                onPropertyChanged();
            }
        }
        public ArtPollReply ArtPollReply { get; private set; }
        private PortAddress? outputPortAddress;
        public PortAddress? OutputPortAddress
        {
            get
            {
                return outputPortAddress;
            }
            private set
            {
                if (outputPortAddress == value)
                    return;
                outputPortAddress = value;
                onPropertyChanged();
            }
        }
        private PortAddress? inputPortAddress;
        public PortAddress? InputPortAddress
        {
            get
            {
                return inputPortAddress;
            }
            private set
            {
                if (inputPortAddress == value)
                    return;
                inputPortAddress = value;
                onPropertyChanged();
            }
        }
        private EPortType portType;
        public EPortType PortType
        {
            get
            {
                return portType;
            }
            private set
            {
                if (portType == value)
                    return;
                portType = value;
                onPropertyChanged();
            }
        }
        private EGoodOutput goodOutput;
        public EGoodOutput GoodOutput
        {
            get
            {
                return goodOutput;
            }
            private set
            {
                if (goodOutput == value)
                    return;
                goodOutput = value;
                onPropertyChanged();
            }
        }
        private EGoodInput goodInput;
        public EGoodInput GoodInput
        {
            get
            {
                return goodInput;
            }
            private set
            {
                if (goodInput == value)
                    return;
                goodInput = value;
                onPropertyChanged();
            }
        }

        private bool isRDMCapable;
        public bool IsRDMCapable
        {
            get
            {
                return isRDMCapable;
            }
            private set
            {
                if (isRDMCapable == value)
                    return;
                isRDMCapable = value;
                onPropertyChanged();
            }
        }

        private ConcurrentDictionary<RDMUID, RDMUID_ReceivedBag> knownRDMUIDs = new ConcurrentDictionary<RDMUID, RDMUID_ReceivedBag>();
        public IReadOnlyCollection<RDMUID_ReceivedBag> KnownRDMUIDs;
        public event EventHandler<RDMUID_ReceivedBag> RDMUIDReceived;
        public event EventHandler<byte[]> RDMMessageReceived;

        public event PropertyChangedEventHandler PropertyChanged;

        private void onPropertyChanged([CallerMemberName] string membername = "")
        {
            onPropertyChanged( new PropertyChangedEventArgs(membername));
        }
        private void onPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            try
            {
                PropertyChanged?.Invoke(this, eventArgs);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }


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
            ID = getIDOf(artPollReply);
            IpAddress = artPollReply.OwnIp;
            BindIndex = artPollReply.BindIndex;
            PortIndex = portIndex;
            processArtPollReply(artPollReply);
            KnownRDMUIDs = knownRDMUIDs.Values.ToList().AsReadOnly();
        }
        public static string getIDOf(ArtPollReply artPollReply)
        {
            return $"{RemoteClient.getIDOf(artPollReply)}==>{artPollReply.BindIndex}";
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