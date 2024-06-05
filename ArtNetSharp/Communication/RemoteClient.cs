using Microsoft.Extensions.Logging;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ArtNetSharp.Communication
{
    public sealed class RemoteClient : INotifyPropertyChanged
    {
        private static readonly ILogger Logger = ApplicationLogging.CreateLogger<RemoteClient>();
        public readonly string ID;
        public readonly MACAddress MacAddress;
        private IPv4Address ipAddress;
        public IPv4Address IpAddress
        {
            get
            {
                return ipAddress;
            }
            set
            {
                if (ipAddress == value)
                    return;

                ipAddress = value;
                onPropertyChanged();
            }
        }
        private string shortName;
        public string ShortName
        {
            get
            {
                return shortName;
            }
            set
            {
                if (shortName == value)
                    return;

                shortName = value;
                onPropertyChanged();
            }
        }
        private string longName;
        public string LongName
        {
            get
            {
                return longName;
            }
            set
            {
                if (longName == value)
                    return;

                longName = value;
                onPropertyChanged();
            }
        }

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
        internal bool Timouted() // Spec 1.4dd page 12, doubled to allow one lost reply (6s is allowad, for some delay i add 2500 ms)
        {
            var now = DateTime.UtcNow.AddSeconds(-3);
            return LastSeen <= now;
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
        private bool isLLRPCapable;
        public bool IsLLRPCapable
        {
            get
            {
                return isLLRPCapable;
            }
            private set
            {
                if (isLLRPCapable == value)
                    return;
                isLLRPCapable = value;
                onPropertyChanged();
            }
        }
        private bool isDHCPCapable;
        public bool IsDHCPCapable
        {
            get
            {
                return isDHCPCapable;
            }
            private set
            {
                if (isDHCPCapable == value)
                    return;
                isDHCPCapable = value;
                onPropertyChanged();
            }
        }
        private bool isWebConfigurationCapable;
        public bool IsWebConfigurationCapable
        {
            get
            {
                return isWebConfigurationCapable;
            }
            private set
            {
                if (isWebConfigurationCapable == value)
                    return;
                isWebConfigurationCapable = value;
                onPropertyChanged();
            }
        }
        private bool isArtNet4Capable;
        public bool IsArtNet4Capable
        {
            get
            {
                return isArtNet4Capable;
            }
            private set
            {
                if (isArtNet4Capable == value)
                    return;
                isArtNet4Capable = value;
                onPropertyChanged();
            }
        }
        private bool isSACNCapable;
        public bool IsSACNCapable
        {
            get
            {
                return isSACNCapable;
            }
            private set
            {
                if (isSACNCapable == value)
                    return;
                isSACNCapable = value;
                onPropertyChanged();
            }
        }

        private ArtPollReply root;
        public ArtPollReply Root
        {
            get
            {
                return root;
            }
            set
            {
                if (ArtPollReply.Equals(root, value))
                    return;

                root = value;
                onPropertyChanged();
                this.IpAddress = root.OwnIp;
                this.ShortName = root.ShortName;
                this.LongName = root.LongName;
                this.IsRDMCapable = root.Status.RDM_Supported;
                this.IsLLRPCapable = root.Status.NodeSupportLLRP;
                this.IsDHCPCapable = root.Status.DHCP_ConfigurationSupported;
                this.IsWebConfigurationCapable = root.Status.WebConfigurationSupported;
                this.IsArtNet4Capable = root.Status.PortAddressBitResolution == NodeStatus.EPortAddressBitResolution._15Bit;
                this.IsSACNCapable = root.Status.NodeSupportArtNet_sACN_Switching;
            }
        }
        private readonly ConcurrentDictionary<int, RemoteClientPort> ports = new ConcurrentDictionary<int, RemoteClientPort>();
        public IReadOnlyCollection<RemoteClientPort> Ports { get; private set; }
        public event EventHandler<RemoteClientPort> PortDiscovered;
        public event EventHandler<RemoteClientPort> PortTimedOut;

        private readonly ConcurrentDictionary<UID, RDMUID_ReceivedBag> knownRDMUIDs = new ConcurrentDictionary<UID, RDMUID_ReceivedBag>();
        private readonly ConcurrentDictionary<EDataRequest, object> artDataCache = new ConcurrentDictionary<EDataRequest, object>();
        public IReadOnlyCollection<RDMUID_ReceivedBag> KnownRDMUIDs;
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

        public IReadOnlyDictionary<EDataRequest, object> ArtDataCache
        {
            get
            {
                return artDataCache;
            }
        }


        private AbstractInstance instance;
        internal AbstractInstance Instance
        {
            get
            {
                return instance;
            }
            set
            {
                if (value == null)
                    return;
                if (instance != value)
                    instance = value;

                _ = PollArtData();
            }
        }

        public RemoteClient(in ArtPollReply artPollReply)
        {
            AbstractInstance.sendPollThreadBag.SendArtPollEvent += OnSendArtPoll;
            seen();
            ID = getIDOf(artPollReply);
            MacAddress = artPollReply.MAC;
            IpAddress = artPollReply.OwnIp;
            seen();
            processArtPollReply(artPollReply);
            seen();
        }
        private async void OnSendArtPoll(object sender, EventArgs e)
        {
            await Task.Delay(3000);

            var timoutedPorts = ports.Where(p => p.Value.Timouted());
            if (timoutedPorts.Count() != 0)
            {
                timoutedPorts = timoutedPorts.ToList();
                foreach (var port in timoutedPorts)
                {
                    if (!ports.TryRemove(port.Key, out _))
                        Logger.LogWarning($"Can't remove RemoteClientPort ({port.Key}) from ConcurrentDictionary");
                    _ = Task.Run(() => PortTimedOut?.InvokeFailSafe(this, port));
                }
            }
            Ports = ports.Select(p => p.Value).ToList().AsReadOnly();
        }
        private void seen()
        {
            LastSeen = DateTime.UtcNow;
        }

        public static string getIDOf(ArtPollReply artPollReply)
        {
            return $"{artPollReply.MAC}#{(ushort)artPollReply.Style}#{artPollReply.ManufacturerCode}#{artPollReply.OemCode}";
        }

        public void processArtPollReply(ArtPollReply artPollReply)
        {
            if (!ID.Equals(getIDOf(artPollReply)))
                return;

            if (artPollReply.BindIndex <= 1)
                Root = artPollReply;

            seen();

            if (artPollReply.Ports == 0)
                return;

            List<Task> tasks = new List<Task>();
            try
            {
                for (byte portIndex = 0; portIndex < artPollReply.Ports; portIndex++)
                {
                    int physicalPort = (Math.Max(0, artPollReply.BindIndex - 1) * artPollReply.Ports) + portIndex;
                    if (ports.TryGetValue(physicalPort, out RemoteClientPort port))
                        port.processArtPollReply(artPollReply);
                    else
                    {
                        port = new RemoteClientPort(artPollReply, portIndex);
                        if (ports.TryAdd(physicalPort, port))
                        {
                            tasks.Add(Task.Run(() => PortDiscovered?.InvokeFailSafe(this, port)));
                            port.RDMUIDReceived += Port_RDMUIDReceived;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
            seen();
            Ports = ports.Select(p => p.Value).ToList().AsReadOnly();
        }
        public async Task processArtDataReply(ArtDataReply artDataReply)
        {
            seen();
            if (artDataReply.Request == EDataRequest.Poll)
            {
                await QueryArtData();
                return;
            }
            var value = artDataReply.PayloadObject ?? artDataReply.Data;
            artDataCache.AddOrUpdate(artDataReply.Request, value, (a, b) => value);
        }

        private async Task PollArtData()
        {
            if (Instance is ControllerInstance)
            {
                using ArtData artData = new ArtData(instance.OEMProductCode, instance.ESTAManufacturerCode);
                await Instance.ArtNetInstance.TrySendPacket(artData, IpAddress);
            }
        }
        private async Task QueryArtData()
        {
            if (Instance is not ControllerInstance)
                return;

            EDataRequest[] todo = new[] { EDataRequest.UrlProduct, EDataRequest.UrlSupport, EDataRequest.UrlUserGuide, EDataRequest.UrlPersUdr, EDataRequest.UrlPersGdtf };
            foreach (EDataRequest req in todo)
            {
                using ArtData artData = new ArtData(instance.OEMProductCode, instance.ESTAManufacturerCode, req);
                await Instance.ArtNetInstance.TrySendPacket(artData, IpAddress);
            }
        }

        private void Port_RDMUIDReceived(object sender, RDMUID_ReceivedBag bag)
        {
            knownRDMUIDs.AddOrUpdate(bag.Uid, bag, (x, y) => bag);
            RDMUIDReceived?.InvokeFailSafe(this, bag);
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
        public UID[] GetReceivedRDMUIDs()
        {
            return KnownRDMUIDs.Where(k => !k.Timouted()).Select(k => k.Uid).ToArray();
        }

        public override string ToString()
        {
            return $"{Root.LongName} / {Root.ShortName} ({Root.Ports}) - IP:{Root.OwnIp} MAC: {Root.MAC}";
        }
    }
}