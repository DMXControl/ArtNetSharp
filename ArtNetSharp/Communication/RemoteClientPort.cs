using Microsoft.Extensions.Logging;
using org.dmxc.wkdt.Light.RDM;
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
        private static readonly ILogger Logger = ApplicationLogging.CreateLogger<RemoteClientPort>();
        public readonly IPv4Address IpAddress;
        public readonly string ID;
        public readonly byte BindIndex;
        public readonly byte PortIndex;
        public readonly int PhysicalPort;
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
        private void seen()
        {
            LastSeen = DateTime.UtcNow;
        }
        internal bool Timouted()// Spec 1.4dd page 12, doubled to allow one lost reply (6s is allowad, for some delay i add 500 ms)
        {
            var now = DateTime.UtcNow.AddSeconds(-6);
            return LastSeen <= now;
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
        private GoodOutput goodOutput;
        public GoodOutput GoodOutput
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
        private GoodInput goodInput;
        public GoodInput GoodInput
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

        private readonly ConcurrentDictionary<UID, RDMUID_ReceivedBag> knownControllerRDMUIDs = new ConcurrentDictionary<UID, RDMUID_ReceivedBag>();
        public IReadOnlyCollection<RDMUID_ReceivedBag> KnownControllerRDMUIDs;
        private readonly ConcurrentDictionary<UID, RDMUID_ReceivedBag> knownResponderRDMUIDs = new ConcurrentDictionary<UID, RDMUID_ReceivedBag>();
        public IReadOnlyCollection<RDMUID_ReceivedBag> KnownResponderRDMUIDs;
        public event EventHandler<RDMUID_ReceivedBag> RDMUIDReceived;

        public event PropertyChangedEventHandler PropertyChanged;

        private void onPropertyChanged([CallerMemberName] string membername = "")
        {
            onPropertyChanged(new PropertyChangedEventArgs(membername));
        }
        private void onPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            try
            {
                PropertyChanged?.InvokeFailSafe(this, eventArgs);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        public RemoteClientPort(in ArtPollReply artPollReply, byte portIndex = 0)
        {
            seen();
            ID = getIDOf(artPollReply, portIndex);
            IpAddress = artPollReply.OwnIp;
            BindIndex = artPollReply.BindIndex;
            PortIndex = portIndex;
            PhysicalPort = ((BindIndex - 1) * artPollReply.Ports) + portIndex;
            processArtPollReply(artPollReply);
            KnownControllerRDMUIDs = new List<RDMUID_ReceivedBag>();
            KnownResponderRDMUIDs = new List<RDMUID_ReceivedBag>();
        }
        public static string getIDOf(ArtPollReply artPollReply, byte portIndex)
        {
            int physicalPort = ((artPollReply.BindIndex - 1) * artPollReply.Ports) + portIndex;
            return $"{RemoteClient.getIDOf(artPollReply)}==>{physicalPort}";
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
            seen();

            PortType = artPollReply.PortTypes[PortIndex];
            GoodOutput = artPollReply.GoodOutput[PortIndex];
            GoodInput = artPollReply.GoodInput[PortIndex];
            var output = artPollReply.OutputUniverses[PortIndex];
            var input = artPollReply.InputUniverses[PortIndex];
            IsRDMCapable = artPollReply.Status.RDM_Supported && !GoodOutput.RDMisDisabled;

            if (PortType.HasFlag(EPortType.OutputFromArtNet))
            {
                if (output is Universe outputUniverse)
                    OutputPortAddress = new PortAddress(artPollReply.Net, artPollReply.Subnet, outputUniverse);
                else if (output is Address outputAddress)
                    OutputPortAddress = new PortAddress(artPollReply.Net, outputAddress);
            }
            else
                OutputPortAddress = null;

            if (PortType.HasFlag(EPortType.InputToArtNet))
            {
                if (input is Universe inputUniverse)
                    InputPortAddress = new PortAddress(artPollReply.Net, artPollReply.Subnet, inputUniverse);
                else if (input is Address inputAddress)
                    InputPortAddress = new PortAddress(artPollReply.Net, inputAddress);
            }
            else
                InputPortAddress = null;
            seen();
        }
        private void addControllerRdmUID(UID rdmuid)
        {
            if (knownControllerRDMUIDs.TryGetValue(rdmuid, out RDMUID_ReceivedBag bag))
                bag.Seen();
            else
            {
                bag = new RDMUID_ReceivedBag(rdmuid);
                if (knownControllerRDMUIDs.TryAdd(rdmuid, bag))
                    Logger.LogTrace($"{IpAddress}#{BindIndex} Cached Controller UID: {bag.Uid}");
            }
            KnownControllerRDMUIDs = knownControllerRDMUIDs.Values.ToList().AsReadOnly();
        }
        internal void AddResponderRdmUIDs(params UID[] rdmuids)
        {
            if (rdmuids.Length == 0)
                return;

            foreach (UID rdmuid in rdmuids)
            {
                if (knownResponderRDMUIDs.TryGetValue(rdmuid, out RDMUID_ReceivedBag bag))
                    bag.Seen();
                else
                {
                    bag = new RDMUID_ReceivedBag(rdmuid);
                    if (knownResponderRDMUIDs.TryAdd(rdmuid, bag))
                    {
                        RDMUIDReceived?.InvokeFailSafe(this, bag);
                        Logger.LogTrace($"{IpAddress}#{BindIndex} Cached Responder UID: {bag.Uid}");
                    }
                }
            }
            KnownResponderRDMUIDs = knownResponderRDMUIDs.Values.ToList().AsReadOnly();
        }
        public void RemoveOutdatedRdmUIDs()
        {
            var outdated = knownResponderRDMUIDs.Where(uid => uid.Value.Timouted()).ToList();
            bool removed = false;
            foreach (var remove in outdated)
                removed |= knownResponderRDMUIDs.TryRemove(remove.Key, out _);
            if (removed)
                KnownResponderRDMUIDs = knownResponderRDMUIDs.Values.ToList().AsReadOnly();
        }
        public UID[] GetReceivedRDMUIDs()
        {
            return KnownResponderRDMUIDs.Where(k => !k.Timouted()).Select(k => k.Uid).ToArray();
        }

        internal void ProcessArtRDM(ArtRDM artRDM)
        {
            if (!artRDM.RDMMessage.Command.HasFlag(ERDM_Command.RESPONSE))
            {
                if (artRDM.Source.IsValidDeviceUID)
                    addControllerRdmUID(artRDM.Source);

                return;
            }
            if (!KnownResponderRDMUIDs.Any(k => k.Uid.Equals(artRDM.Source)))
                return;

            seen();
        }

        public override string ToString()
        {
            return $"{nameof(RemoteClientPort)}: {IpAddress}#{BindIndex},{PortIndex}";
        }
    }
}