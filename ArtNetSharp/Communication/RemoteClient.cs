using Microsoft.Extensions.Logging;
using RDMSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ArtNetSharp.Communication
{
    public class RemoteClient : INotifyPropertyChanged
    {
        private static ILogger Logger = ApplicationLogging.CreateLogger<RemoteClient>();
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IpAddress)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShortName)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LongName)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Root)));
                this.IpAddress = root.OwnIp;
                this.ShortName = root.ShortName;
                this.LongName = root.LongName;
            }
        }
        private ConcurrentDictionary<byte, RemoteClientPort> ports= new ConcurrentDictionary<byte, RemoteClientPort>();
        public IReadOnlyCollection<RemoteClientPort> Ports { get; private set; }
        public event EventHandler<RemoteClientPort> PortDiscovered;
        public event EventHandler<RemoteClientPort> PortTimedOut;

        private ConcurrentDictionary<RDMUID, RDMUID_ReceivedBag> knownRDMUIDs = new ConcurrentDictionary<RDMUID, RDMUID_ReceivedBag>();
        private ConcurrentDictionary<EDataRequest, object> artDataCache = new ConcurrentDictionary<EDataRequest, object>();
        public IReadOnlyCollection<RDMUID_ReceivedBag> KnownRDMUIDs;
        public event EventHandler<RDMUID_ReceivedBag> RDMUIDReceived;

        public event PropertyChangedEventHandler PropertyChanged;

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

        public DateTime LastSeen { get; private set; }

        public RemoteClient(in MACAddress macAddress, in IPv4Address ipAddress)
        {
            MacAddress = macAddress;
            IpAddress = ipAddress;
            LastSeen = DateTime.UtcNow;
        }
        public RemoteClient(in ArtPollReply artPollReply) : this(artPollReply.MAC, artPollReply.OwnIp)
        {
            processArtPollReply(artPollReply);
        }

        public void processArtPollReply(ArtPollReply artPollReply)
        {
            if (!MacAddress.Equals(artPollReply.MAC))
                return;

            if (artPollReply.BindIndex <= 1)
                Root = artPollReply;

            if (artPollReply.Ports == 0)
                return;
            try
            {
                RemoteClientPort port = null;
                if (ports.TryGetValue(artPollReply.BindIndex, out port))
                    port.processArtPollReply(artPollReply);
                else
                {
                    port = new RemoteClientPort(artPollReply);
                    if (ports.TryAdd(port.BindIndex, port))
                    {
                        PortDiscovered?.Invoke(this, port);
                        port.RDMUIDReceived += Port_RDMUIDReceived;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }

            LastSeen = DateTime.UtcNow;
            var deadline = LastSeen.AddSeconds(-6); // Spec 1.4dd page 12, doubled to allow one lost reply
            var timoutedPorts = ports.Where(p => p.Value.LastSeen < deadline);
            if (timoutedPorts.Count() != 0)
            {
                timoutedPorts = timoutedPorts.ToList();
                foreach (var port in timoutedPorts)
                {
                    ports.TryRemove(port.Key, out _);
                    PortTimedOut?.Invoke(this, port.Value);
                }
            }
            Ports = ports.Select(p => p.Value).ToList().AsReadOnly();
        }
        public async Task processArtDataReply(ArtDataReply artDataReply)
        {
            if(artDataReply.Request == EDataRequest.Poll)
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
                await ArtNet.Instance.TrySendPacket(new ArtData(instance.OEMProductCode, instance.ESTAManufacturerCode), IpAddress);
        }
        private async Task QueryArtData()
        {
            if (!(Instance is ControllerInstance))
                return;

            EDataRequest[] todo = new[] { EDataRequest.UrlProduct, EDataRequest.UrlSupport, EDataRequest.UrlUserGuide, EDataRequest.UrlPersUdr, EDataRequest.UrlPersGdtf };
            foreach (EDataRequest req in todo)
                await ArtNet.Instance.TrySendPacket(new ArtData(instance.OEMProductCode, instance.ESTAManufacturerCode, req), IpAddress);
        }

        private void Port_RDMUIDReceived(object sender, RDMUID_ReceivedBag bag)
        {
            knownRDMUIDs.AddOrUpdate(bag.Uid, bag, (x, y) => bag);
            RDMUIDReceived?.Invoke(this, bag);
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

        public override string ToString()
        {
            return $"{Root.LongName} / {Root.ShortName} ({Root.Ports}) - IP:{Root.OwnIp} MAC: {Root.MAC}";
        }
    }
}