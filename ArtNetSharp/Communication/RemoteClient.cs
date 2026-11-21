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

namespace ArtNetSharp.Communication;

public sealed class RemoteClient : INotifyPropertyChanged
{
    private static readonly ILogger Logger = Logging.CreateLogger<RemoteClient>();
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
    /// <summary>Spec 1.4dd page 12, doubled to allow one lost reply. Settable for tests only.</summary>
    internal static TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(6);
    internal bool Timouted()
    {
        return LastSeen <= DateTime.UtcNow - Timeout;
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
    /// <summary>Never null: empty until the first ArtPollReply with ports arrived (was null before, breaking enumeration).</summary>
    public IReadOnlyCollection<RemoteClientPort> Ports { get; private set; } = Array.Empty<RemoteClientPort>();
    public event EventHandler<RemoteClientPort> PortDiscovered;
    public event EventHandler<RemoteClientPort> PortTimedOut;

    private readonly ConcurrentDictionary<UID, RDMUID_ReceivedBag> knownRDMUIDs = new ConcurrentDictionary<UID, RDMUID_ReceivedBag>();
    private readonly ConcurrentDictionary<EDataRequest, object> artDataCache = new ConcurrentDictionary<EDataRequest, object>();
    /// <summary>Never null (GetReceivedRDMUIDs threw a NullReferenceException before the first RDM device was seen).</summary>
    public IReadOnlyCollection<RDMUID_ReceivedBag> KnownRDMUIDs = Array.Empty<RDMUID_ReceivedBag>();
    public event EventHandler<RDMUID_ReceivedBag> RDMUIDReceived;

    public event PropertyChangedEventHandler PropertyChanged;
    private void onPropertyChanged([CallerMemberName] string membername = "")
    {
        onPropertyChanged(new PropertyChangedEventArgs(membername));
    }
    private void onPropertyChanged(PropertyChangedEventArgs eventArgs)
    {
        EventDispatcher.Raise(events, () => PropertyChanged?.InvokeFailSafe(this, eventArgs));
    }

    /// <summary>Event queue of the owning instance (null if this client was created standalone).</summary>
    private readonly EventDispatcher events;

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

    public RemoteClient(in ArtPollReply artPollReply) : this(artPollReply, null)
    {
    }
    internal RemoteClient(in ArtPollReply artPollReply, EventDispatcher events)
    {
        this.events = events; // first: processArtPollReply below already raises events
        // Port timeouts are checked by the owning instance (AbstractInstance -> RemoveTimedOutPorts),
        // not via a subscription to the static poll event (that kept every RemoteClient alive forever).
        seen();
        ID = getIDOf(artPollReply);
        MacAddress = artPollReply.MAC;
        IpAddress = artPollReply.OwnIp;
        seen();
        processArtPollReply(artPollReply);
        seen();
    }
    /// <summary>
    /// Timed-out ports are parked here instead of being deleted (same approach as timed-out RemoteClients in
    /// AbstractInstance). When the port reports again, the same object is revived, so its state (known RDM
    /// responders, PropertyChanged bindings, references held by the application) survives a short outage.
    /// Deleting them was the reason the former timeout had been disabled ("causes problems in debug"): a breakpoint
    /// longer than the timeout made all ports time out at once and come back as new, empty objects.
    /// </summary>
    private readonly ConcurrentDictionary<int, RemoteClientPort> portsTimedOut = new ConcurrentDictionary<int, RemoteClientPort>();

    /// <summary>Moves timed-out ports out of <see cref="Ports"/> and raises <see cref="PortTimedOut"/> once per port.</summary>
    internal void RemoveTimedOutPorts()
    {
        bool removed = false;
        foreach (var entry in ports.Where(p => p.Value.Timouted()).ToList())
        {
            if (!ports.TryRemove(entry.Key, out RemoteClientPort port))
                continue;
            portsTimedOut[entry.Key] = port;
            removed = true;
            Logger.LogInformation($"Port timeout: {port.ID}");
            EventDispatcher.Raise(events, () => PortTimedOut?.InvokeFailSafe(this, port));
        }
        if (removed)
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

        try
        {
            for (byte portIndex = 0; portIndex < artPollReply.Ports; portIndex++)
            {
                int physicalPort = (Math.Max(0, artPollReply.BindIndex - 1) * artPollReply.Ports) + portIndex;
                if (ports.TryGetValue(physicalPort, out RemoteClientPort port))
                    port.processArtPollReply(artPollReply);
                else if (portsTimedOut.TryRemove(physicalPort, out port))
                {
                    // Revive the same object (keeps its RDM responders and bindings).
                    port.processArtPollReply(artPollReply);
                    if (ports.TryAdd(physicalPort, port))
                        EventDispatcher.Raise(events, () => PortDiscovered?.InvokeFailSafe(this, port));
                }
                else
                {
                    port = new RemoteClientPort(artPollReply, portIndex, events);
                    if (ports.TryAdd(physicalPort, port))
                    {
                        // Internal, synchronous hook (not the public event): the library's own state must not
                        // depend on the event queue, which an application handler may delay.
                        port.RdmUidAdded = Port_RDMUIDReceived;
                        port.RdmUidRemoved = Port_RDMUIDRemoved;
                        EventDispatcher.Raise(events, () => PortDiscovered?.InvokeFailSafe(this, port));
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

    private void Port_RDMUIDReceived(RDMUID_ReceivedBag bag)
    {
        knownRDMUIDs.AddOrUpdate(bag.Uid, bag, (x, y) => bag);
        KnownRDMUIDs = knownRDMUIDs.Values.ToList().AsReadOnly(); // the public list was never updated here
        EventDispatcher.Raise(events, () => RDMUIDReceived?.InvokeFailSafe(this, bag));
    }
    private void Port_RDMUIDRemoved(RDMUID_ReceivedBag bag)
    {
        if (knownRDMUIDs.TryRemove(bag.Uid, out _))
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
    public UID[] GetReceivedRDMUIDs()
    {
        return KnownRDMUIDs.Where(k => !k.Timouted()).Select(k => k.Uid).ToArray();
    }

    public override string ToString()
    {
        if (Root == null)
            return base.ToString() + "NULL";
        return $"{Root.LongName} / {Root.ShortName} ({Root.Ports}) - IP:{Root.OwnIp} MAC: {Root.MAC}";
    }
}