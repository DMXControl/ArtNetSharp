using ArtNetSharp.Messages.Interfaces;
using ArtNetSharp.Misc;
using Microsoft.Extensions.Logging;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("ArtNetTests")]
namespace ArtNetSharp.Communication;

public abstract class AbstractInstance : IInstance
{
    protected static ILogger Logger { get; private set; } = null;
    protected bool IsDisposed { get; private set; }
    protected bool IsDisposing { get; private set; }
    bool IDisposableExtended.IsDisposed => IsDisposed;
    bool IDisposableExtended.IsDisposing => IsDisposing;

    /// <summary>
    /// The ArtNet this instance is currently added to (set by <see cref="ArtNet.AddInstance"/> / <see cref="ArtNet.RemoveInstance"/>).
    /// Replaces a lookup in <see cref="ArtNet.Instances"/>, which allocated two lists on every check (every packet, send and DMX tick).
    /// </summary>
    internal volatile ArtNet RegisteredIn;
    public bool IsDeactivated
    {
        get
        {
            var artNet = ArtNetInstance;
            return artNet == null || !ReferenceEquals(RegisteredIn, artNet);
        }
    }

    private readonly Random _random;
    internal ArtNet ArtNetInstance;
    public string Name { get; set; }
    public string ShortName { get; set; }
    public abstract EStCodes EstCodes { get; }

    private uint artPollReplyCounter = 0;

    public virtual byte MajorVersion { get; } = 1;
    public virtual byte MinorVersion { get; } = 0;
    public virtual bool EnableDmxOutput { get; } = true;
    protected virtual bool EnableSync { get; } = true;

    public virtual ushort ESTAManufacturerCode { get; } = Constants.DEFAULT_ESTA_MANUFACTURER_CODE;
    public virtual ushort OEMProductCode { get; } = Constants.DEFAULT_OEM_CODE;

    protected virtual bool SendArtPollBroadcast { get; } = false;
    protected virtual bool SendArtPollTargeted { get; } = false;
    protected virtual bool SendArtData { get; } = false;
    protected virtual bool SupportRDM { get; } = false;

    protected virtual string UrlProduct { get; }
    protected virtual string UrlUserGuid { get; }
    protected virtual string UrlSupport { get; }
    protected virtual string UrlPersonalityUDR { get; }
    protected virtual string UrlPersonalityGDTF { get; }

    private readonly ConcurrentDictionary<UID, ControllerRDMUID_Bag> knownControllerRDMUIDs = new ConcurrentDictionary<UID, ControllerRDMUID_Bag>();
    public virtual UID UID { get; } = UID.Empty;

    /// <summary>
    /// Copy-on-write: the array is never modified, Add/RemovePortConfig replace it under <see cref="portConfigsWriteLock"/>.
    /// The receive and DMX send loops read it lock-free and can no longer hit "Collection was modified".
    /// </summary>
    private volatile PortConfig[] portConfigs = Array.Empty<PortConfig>();
    private readonly object portConfigsWriteLock = new object();
    public ReadOnlyCollection<PortConfig> PortConfigs => Array.AsReadOnly(portConfigs);

    /// <summary>Received DMX per source. Spec (Data Merging): a source is the IP address plus the Physical port.</summary>
    private readonly ConcurrentDictionary<(PortAddress Port, IPv4Address Ip, byte Physical), DMXReceiveBag> receivedDMXBuffer = new ConcurrentDictionary<(PortAddress, IPv4Address, byte), DMXReceiveBag>();

    /// <summary>Spec (Data Merging): a failed source is held in the merge for 10 seconds.</summary>
    internal static TimeSpan MergeSourceTimeout { get; set; } = TimeSpan.FromSeconds(10); // settable for tests only
    /// <summary>Spec (Data Merging): merging is limited to two sources, any additional sources are ignored.</summary>
    internal const int MaxMergeSources = 2;
    private static readonly GoodOutput MERGING = new GoodOutput(mergingArtNetData: true);

    /// <summary>
    /// The sources of <paramref name="portAddress"/> that take part in the merge:
    /// those that sent within <see cref="MergeSourceTimeout"/>. If all of them failed, the output holds
    /// the last merge result, i.e. the sources that were active when the last packet arrived.
    /// </summary>
    private List<DMXReceiveBag> getMergeSources(PortAddress portAddress, DateTime utcNow)
    {
        var bags = new List<DMXReceiveBag>(MaxMergeSources);
        DateTime newest = DateTime.MinValue;
        foreach (var kv in receivedDMXBuffer) // enumerating the dictionary itself avoids the snapshot list of .Values
            if (kv.Key.Port == portAddress)
            {
                var bag = kv.Value;
                bags.Add(bag);
                if (bag.LastUpdate > newest)
                    newest = bag.LastUpdate;
            }

        DateTime reference = utcNow - newest < MergeSourceTimeout ? utcNow : newest;
        bags.RemoveAll(b => reference - b.LastUpdate >= MergeSourceTimeout);
        return bags;
    }
    private readonly ConcurrentDictionary<RDM_TransactionID, RDMMessage> artRDMdeBumbReceive = new();

    private readonly ConcurrentDictionary<IPv4Address, GatewayRDMFiFoQueue> artRDMgatewayQueues = new();

    private readonly SemaphoreSlim pollReplyProcessSemaphoreSlim = new SemaphoreSlim(1);

    private readonly struct RDM_TransactionID : IEquatable<RDM_TransactionID>
    {
        private readonly byte Transaction;
        private readonly UID Controller;
        private readonly UID Responder;

        public RDM_TransactionID(byte transaction, UID controller, UID responder)
        {
            Transaction = transaction;
            Controller = controller;
            Responder = responder;
        }
        public override string ToString() => $"{Transaction} C: {Controller} R: {Responder}";

        public override bool Equals(object obj) => obj is RDM_TransactionID iD && Equals(iD);

        public bool Equals(RDM_TransactionID other)
        {
            return Transaction == other.Transaction &&
                   Controller.Equals(other.Controller) &&
                   Responder.Equals(other.Responder);
        }

        public override int GetHashCode() => Transaction.GetHashCode() + Controller.GetHashCode() + Responder.GetHashCode();
    }
    private class DMXReceiveBag : IDisposable
    {
        public byte[] Data { get; private set; } = new byte[512];
        public byte Sequence { get; private set; } = byte.MaxValue;
        public readonly PortAddress PortAddress;
        public readonly IPv4Address Source;
        /// <summary>Spec (Data Merging): a source is identified by IP address and Physical port.</summary>
        public readonly byte Physical;
        public DateTime LastUpdate { get; private set; }

        public bool IsDisposed;
        public bool IsDisposing;
        public DMXReceiveBag(ArtDMX artDMX, IPv4Address source)
        {
            PortAddress = new PortAddress(artDMX.Net, artDMX.Address);
            Source = source;
            Physical = artDMX.Physical;
            Update(artDMX, source);
        }
        internal bool Update(ArtDMX artDMX, IPv4Address source)
        {
            if (IsDisposed || IsDisposing)
                return false;

            if (Source != source || Physical != artDMX.Physical)
                return false;

            var utcNow = DateTime.UtcNow;
            // After a pause the old sequence number says nothing about the new packets (e.g. the source restarted).
            bool? check = (utcNow - LastUpdate) >= SequenceResetTime ? null : checkSequence(Sequence, artDMX.Sequence);
            if (check == false)
            {
                // A real re-ordering on the network affects single packets. If "old" packets keep coming,
                // the source restarted or jumped (e.g. a console reboot within SequenceResetTime): resync to it.
                if (++rejectedInARow < MaxRejectedInARow)
                    return false;
            }
            rejectedInARow = 0;

            Sequence = artDMX.Sequence;

            Array.Copy(artDMX.Data, 0, Data, 0, Math.Min(artDMX.Data.Length, Data.Length));

            LastUpdate = utcNow;
            return true;
        }

        internal static readonly TimeSpan SequenceResetTime = TimeSpan.FromSeconds(1);
        internal const int MaxRejectedInARow = 4;
        private int rejectedInARow;

        private bool? checkSequence(byte _old, byte _new)
        {
            if (IsDisposed || IsDisposing)
                return null;
            return IsNewerSequence(_old, _new);
        }

        /// <summary>
        /// Spec (ArtDmx, Sequence): 0x01..0xff lets the receiver re-sequence packets, 0x00 disables it.
        /// Uses serial number arithmetic: a packet counts as newer if it is up to 127 steps ahead
        /// (incl. the wrap 0xff -> 0x01), and as old (out of order / duplicate) if it is up to 128 steps behind.
        /// </summary>
        /// <returns>null if sequencing is disabled for this comparison, otherwise whether <paramref name="_new"/> is newer.</returns>
        internal static bool? IsNewerSequence(byte _old, byte _new)
        {
            if (_new == 0 || _old == 0)
                return null; // sequencing disabled (by this packet, or no reference yet)

            int ahead = (_new - _old + 256) % 256;
            return ahead != 0 && ahead < 128;
        }

        public void Dispose()
        {
            if (IsDisposed || IsDisposing)
                return;

            IsDisposing = true;
            GC.SuppressFinalize(Data);
            Data = null;
            Sequence = 0;
            IsDisposed = true;
            IsDisposing = false;
            GC.SuppressFinalize(this);
        }
    }
    private class DMXSendBag : IDisposable
    {
        public byte[] Data { get; private set; } = new byte[512];
        public bool Updated => LastUpdated > LastSended;
        public byte Sequence { get; private set; }

        public readonly PortAddress PortAddress;
        public DateTime LastUpdated { get; private set; }
        public DateTime LastSended { get; internal set; }

        private SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1);

        public bool IsDisposed;
        public bool IsDisposing;

        /// <summary>Creates an empty (all zero) buffer; data is written via <see cref="Update"/>.</summary>
        public DMXSendBag(PortAddress portAddress)
        {
            PortAddress = portAddress;
        }

        internal async void Update(byte[] data, ushort? destinationIndex = null, ushort? count = null)
        {
            if (IsDisposed || IsDisposing)
                return;

            await SemaphoreSlim?.WaitAsync();
            try
            {
                // Copy data[0..count) to Data[destinationIndex..), clipped to the 512 channels.
                int start = destinationIndex ?? 0;
                int length = Math.Min(Math.Min(count ?? data.Length, data.Length), Data.Length - start);
                if (length > 0)
                    Array.Copy(data, 0, Data, start, length);

                LastUpdated = DateTime.UtcNow;
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }

            SemaphoreSlim?.Release();
        }

        internal byte GetSequence()
        {
            // Spec (ArtDmx, Sequence): incremented in the range 0x01 to 0xff; 0x00 means "sequencing disabled".
            Sequence = Sequence == byte.MaxValue ? (byte)1 : (byte)(Sequence + 1);
            return Sequence;
        }

        public void Dispose()
        {
            if (IsDisposed || IsDisposing)
                return;

            IsDisposing = true;
            GC.SuppressFinalize(Data);
            Data = null;
            Sequence = 0;
            LastUpdated = DateTime.MaxValue;
            LastSended = DateTime.MaxValue;
            SemaphoreSlim?.Dispose();
            SemaphoreSlim = null;
            IsDisposed = true;
            IsDisposing = false;
            GC.SuppressFinalize(this);
        }
    }
    private readonly ConcurrentDictionary<PortAddress, DMXSendBag> sendDMXBuffer = new ConcurrentDictionary<PortAddress, DMXSendBag>();
    private readonly SemaphoreSlim semaphoreSlimAddRemoteClient = new SemaphoreSlim(1, 1);

    private readonly ConcurrentDictionary<string, RemoteClient> remoteClients = new ConcurrentDictionary<string, RemoteClient>();
    private readonly ConcurrentDictionary<string, RemoteClient> remoteClientsTimeouted = new ConcurrentDictionary<string, RemoteClient>();
    public IReadOnlyCollection<RemoteClient> RemoteClients { get; private set; } = new List<RemoteClient>();
    public IReadOnlyCollection<RemoteClientPort> RemoteClientsPorts => remoteClients?.Where(rc => rc.Value?.Ports != null).ToList().SelectMany(rc => rc.Value.Ports).ToList().AsReadOnly();

    public event EventHandler<PortAddress> DMXReceived;
    public event EventHandler SyncReceived;
    public event EventHandler<RemoteClient> RemoteClientDiscovered;
    public event EventHandler<RemoteClient> RemoteClientTimedOut;
    public event EventHandler<ArtTimeCode> TimeCodeReceived;
    public event EventHandler<ArtTimeSync> TimeSyncReceived;

    private readonly ConcurrentDictionary<UID, RDMUID_ReceivedBag> knownRDMUIDs = new ConcurrentDictionary<UID, RDMUID_ReceivedBag>();
    public IReadOnlyCollection<RDMUID_ReceivedBag> KnownRDMUIDs;
    public event EventHandler<RDMUID_ReceivedBag> RDMUIDReceived;
    public event EventHandler<ResponseRDMMessageReceivedEventArgs> ResponseRDMMessageReceived;
    public event EventHandler<RequestRDMMessageReceivedEventArgs> RequestRDMMessageReceived;

    internal static readonly SendPollThreadBag sendPollThreadBag = new SendPollThreadBag();

    internal class SendPollThreadBag
    {
        private static readonly TimeSpan PollPeriod = TimeSpan.FromSeconds(2.7); // Spec 1.4dd page 13

        private readonly Thread sendPollThread;
        public EventHandler SendArtPollEvent;
        public SendPollThreadBag()
        {
            sendPollThread = new Thread(async () =>
            {
                DateTime lastSendPollTime = DateTime.UtcNow;
                while (true)
                {
                    try
                    {
                        TimeSpan elapsed = DateTime.UtcNow - lastSendPollTime;
                        if (elapsed < PollPeriod)
                            await Task.Delay(PollPeriod - elapsed);

                        SendArtPollEvent?.InvokeFailSafe(null, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex);
                    }

                    lastSendPollTime = DateTime.UtcNow;
                    await Task.Delay(300);
                }
            });
            sendPollThread.IsBackground = true;
            sendPollThread.Name = "sendArtPollThread";
            sendPollThread.Priority = ThreadPriority.Highest;
            sendPollThread.Start();
        }
    }

    protected AbstractInstance(ArtNet _artnet)
    {
        _random = new Random();
        ArtNetInstance = _artnet;
        Logger = Logging.CreateLogger(GetType());

        ArtNetInstance.OnInstanceAdded += ArtNet_OnInstanceAdded;
        ArtNetInstance.OnInstanceRemoved += ArtNet_OnInstanceRemoved;

        sendPollThreadBag.SendArtPollEvent += TimerSendPoll_Elapsed;

        Task.Run(sendAllArtDMX);

        KnownRDMUIDs = knownRDMUIDs.Values.ToList().AsReadOnly();
    }

    ~AbstractInstance()
    {
        ((IDisposable)this).Dispose();
    }

    private async void ArtNet_OnInstanceAdded(object sender, AbstractInstance e)
    {
        if (e != this)
            return;

        await triggerSendArtPoll();
    }

    private void ArtNet_OnInstanceRemoved(object sender, AbstractInstance e)
    {
        if (e != this)
            return;
    }

    void IInstance.PacketReceived(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        try
        {
            switch (packet)
            {
                case ArtPoll artPoll:
                    _ = sendArtPollReply(localIp, sourceIp, artPoll);
                    break;
                case ArtPollReply artPollReply:
                    _ = processArtPollReply(artPollReply, localIp, sourceIp);
                    break;
                case ArtData artData:
                    _ = processArtData(artData, sourceIp);
                    break;

                case ArtDMX artDMX:
                    processArtDMX(artDMX, sourceIp);
                    break;
                case ArtSync artSync:
                    SyncReceived?.InvokeFailSafe(this, new EventArgs());
                    break;

                case ArtTodRequest artTodRequest:
                    _ = processArtTodRequest(artTodRequest, sourceIp);
                    break;
                case ArtTodControl artTodControl:
                    _ = processArtTodControl(artTodControl, sourceIp);
                    break;

                case ArtTodData artTodData:
                    processArtTodData(artTodData, sourceIp);
                    break;
                case ArtRDM artRDM:
                    if (artRDM.RDMMessage?.ChecksumValid == true)
                        processArtRDM(artRDM, sourceIp);
                    break;

                case ArtTimeCode artTimeCode:
                    TimeCodeReceived?.InvokeFailSafe(this, artTimeCode);
                    break;
                case ArtTimeSync artTimeSync:
                    TimeSyncReceived?.InvokeFailSafe(this, artTimeSync);
                    break;

                default:
                    OnPacketReceived(packet, localIp, sourceIp);
                    break;
            }
        }
        catch (Exception e)
        {
            Logger?.LogError(e);
        }
    }
    protected abstract void OnPacketReceived(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp);

    #region Send
    protected async Task TrySendPacket(AbstractArtPacketCore packet, IPv4Address destinationIp)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        await ArtNetInstance.TrySendPacket(packet, destinationIp);
    }
    protected async Task TrySendBroadcastPacket(AbstractArtPacketCore packet)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        await ArtNetInstance.TrySendBroadcastPacket(packet);
    }

    private async Task sendArtPoll(PortAddress targetPortTop = default, PortAddress targetPortBottom = default)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        using ArtPoll artPoll = new ArtPoll(OEMProductCode, ESTAManufacturerCode, targetPortTop: targetPortTop, targetPortBottom: targetPortBottom);
        await TrySendBroadcastPacket(artPoll);
    }
    private async Task sendArtPollReply(IPv4Address ownIp, IPv4Address destinationIp, ArtPoll artPoll = null)
    {
        if (IsDisposing || IsDisposed || IsDeactivated || ArtNetInstance == null)
            return;

        try
        {
            await Task.Delay((int)(800 * _random.NextDouble())); //Art-Net 4 Protocol Release V1.4 Document Revision 1.4dh 19/7/2023 - 23 -

            if (IsDisposing || IsDisposed || IsDeactivated || ArtNetInstance == null)
                return;

            MACAddress ownMacAddress = ArtNetInstance.GetMacAdress(ownIp);
            NodeStatus nodeStatus = getOwnNodeStatus();
            NodeReport nodeReport = new NodeReport(ENodeReportCodes.RcPowerOk, "Everything ok.", artPollReplyCounter);
            Net net = 0;
            Subnet subnet = 0;
            List<Task> tasks = new List<Task>();
            var ports = portConfigs.OrderBy(pc => pc.PortAddress).ToList();
            if (artPoll.Flags.HasFlag(EArtPollFlags.EnableTargetedMode))
                ports = ports.Where(pc => pc.PortAddress >= artPoll.TargetPortBottom && pc.PortAddress <= artPoll.TargetPortTop).ToList();

            Task taskRoot = Task.Run(async () =>
            {
                ArtPollReply reply = new ArtPollReply(ownIp,
                                                  null,
                                                  ownMacAddress,
                                                  ShortName,
                                                  Name,
                                                  0,
                                                  nodeStatus,
                                                  MajorVersion,
                                                  MinorVersion,
                                                  0,
                                                  0,
                                                  new object[0],
                                                  new object[0],
                                                  OEMProductCode,
                                                  ESTAManufacturerCode,
                                                  nodeReport: nodeReport,
                                                  style: EstCodes);
                await TrySendPacket(reply, destinationIp);
            });
            tasks.Add(taskRoot);
            if (ports.Count != 0)
            {
                foreach (PortConfig portConfig in ports.OrderBy(p => p.BindIndex).ToList())
                {
                    if (artPoll?.Flags.HasFlag(EArtPollFlags.EnableTargetedMode) ?? false)
                    {
                        ushort pA = portConfig.PortAddress;
                        if (pA > artPoll.TargetPortTop || pA < artPoll.TargetPortBottom)
                            continue;
                    }

                    Task task = Task.Run(async () =>
                    {
                        var goodInput = portConfig.GoodInput;
                        if (portConfig.Input)
                            if (sendDMXBuffer.ContainsKey(portConfig.PortAddress))
                                goodInput = new GoodInput((byte)(goodInput.Byte1 | 0b10000000));
                        ArtPollReply reply = new ArtPollReply(ownIp,
                                                 ownIp,
                                                 ownMacAddress,
                                                 ShortName,
                                                 Name,
                                                 portConfig.BindIndex,
                                                 nodeStatus,
                                                 MajorVersion,
                                                 MinorVersion,
                                                 portConfig.Net,
                                                 portConfig.Subnet,
                                                 (Universe)(portConfig.Output ? portConfig.Universe : 0),
                                                 (Universe)(portConfig.Input ? portConfig.Universe : 0),
                                                 OEMProductCode,
                                                 ESTAManufacturerCode,
                                                 nodeReport,
                                                 portConfig.Type,
                                                 goodInput,
                                                 portConfig.GoodOutput,
                                                 style: EstCodes);

                        await Task.Delay((int)(50 * _random.NextDouble()));
                        await TrySendPacket(reply, destinationIp);
                    });
                    tasks.Add(task);
                }
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
        finally
        {
            artPollReplyCounter++;
        }
    }

    #region Send ArtTimeCode
    public async Task SendArtTimeCode(ArtTimeCode timeCode, IPv4Address? ipAddress = null, bool broadcast = true) => await sendArtTimeCode(timeCode, ipAddress, broadcast);
    private async Task sendArtTimeCode(ArtTimeCode timeCode, IPv4Address? ipAddress = null, bool broadcast = true)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        if (broadcast)
            await TrySendBroadcastPacket(timeCode);
        else
        {
            if (ipAddress.HasValue)
                await TrySendPacket(timeCode, ipAddress.Value);
            else
                throw new ArgumentNullException($"{ipAddress} is not set, and {nameof(broadcast)} is {broadcast}");
        }
    }
    #endregion

    #region Send ArtTimeSync
    public async Task SendArtTimeSync(ArtTimeSync timeSync, IPv4Address? ipAddress = null, bool broadcast = true) => await sendArtTimeSync(timeSync, ipAddress, broadcast);
    private async Task sendArtTimeSync(ArtTimeSync timeSync, IPv4Address? ipAddress = null, bool broadcast = true)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        if (broadcast)
            await TrySendBroadcastPacket(timeSync);
        else
        {
            if (ipAddress.HasValue)
                await TrySendPacket(timeSync, ipAddress.Value);
            else
                throw new ArgumentNullException($"{ipAddress} is not set, and {nameof(broadcast)} is {broadcast}");
        }
    }
    #endregion

    #region Send ArtSync
    public async Task SendArtSync()
    {
        if (EnableSync)
            await sendArtSync();
    }
    private async Task sendArtSync(params IPv4Address[] addresses)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;
        using ArtSync artSync = new ArtSync();
        if (addresses.Length == 0)
            await TrySendBroadcastPacket(artSync);
        else
        {
            var smalArray = addresses.Distinct();
            var networkClients = ArtNetInstance.NetworkClients.ToList();
            var broadcastAddresses = smalArray.Select(a => networkClients.FirstOrDefault(n => Tools.IsInSubnet(a, n.IPv4Mask, n.LocalIpAddress)).BroadcastIpAddress).Distinct().ToList();
            ;
            broadcastAddresses.ForEach(async (b) => await TrySendPacket(artSync, b));
        }
    }
    #endregion

    #region Send ArtDMX
    internal async Task sendArtDMX(RemoteClientPort remoteClientPort, byte sourcePort, byte[] data, byte sequence, bool broadcast = false)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        if (!remoteClientPort.OutputPortAddress.HasValue)
            return;

        PortAddress portAddress = remoteClientPort.OutputPortAddress.Value;
        using ArtDMX artDMX = new ArtDMX(sequence, sourcePort, portAddress.Net, portAddress.Address, data);
        if (broadcast)
            await TrySendBroadcastPacket(artDMX);
        else
            await TrySendPacket(artDMX, remoteClientPort.IpAddress);
    }
    private async Task sendArtDMXBroadcast(PortAddress portAddress, byte sourcePort, byte[] data, byte sequence)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        using ArtDMX artDMX = new ArtDMX(sequence, sourcePort, portAddress.Net, portAddress.Address, data);
        await TrySendBroadcastPacket(artDMX);
    }
    public async Task sendArtDMX(IPv4Address ipAddress, PortAddress portAddress, byte sourcePort, byte[] data, byte sequence)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        using ArtDMX artDMX = new ArtDMX(sequence, sourcePort, portAddress.Net, portAddress.Address, data);
        await TrySendPacket(artDMX, ipAddress);
    }

    /// <summary>Set whenever remote clients/ports may have changed; the send loop then rebuilds its port lookup.</summary>
    private volatile bool outputPortsDirty = true;
    private ILookup<PortAddress, RemoteClientPort> outputPortsByAddress;
    private ILookup<PortAddress, RemoteClientPort> getOutputPortsByAddress()
    {
        if (outputPortsDirty || outputPortsByAddress == null)
        {
            outputPortsDirty = false;
            outputPortsByAddress = (RemoteClientsPorts ?? (IReadOnlyCollection<RemoteClientPort>)Array.Empty<RemoteClientPort>())
                .Where(p => p.OutputPortAddress.HasValue)
                .ToLookup(p => p.OutputPortAddress.Value);
        }
        return outputPortsByAddress;
    }

    private async Task sendAllArtDMX()
    {
        const double dmxRefreshTime = 1000 / 44.0; // Spec 1.4dp: ArtPollReply RefreshRate, max DMX512 rate of 44Hz
        const double dmxKeepAliveTime = 800; // Spec 1.4dp: recommended re-transmit time 800mS to 1000mS
        // Universes whose last transmission is at least this old ride along with the next frame,
        // so keep-alives of different universes converge onto shared frames (one ArtSync each).
        const double keepAliveBatchTime = dmxKeepAliveTime / 2;
        const int interval = (int)(dmxRefreshTime / 3);
        List<Task> sendTasks = new List<Task>();
        HashSet<IPv4Address> sentToIps = new HashSet<IPv4Address>();
        DateTime lastFrame = DateTime.MinValue;
        while (!(IsDisposing || IsDisposed))
        {
            // Prevent CPU loop
            await Task.Delay(interval);

            if (!EnableDmxOutput)
                await Task.Delay(300);
            if (IsDeactivated)
            {
                await Task.Delay(300);
                continue;
            }
            // Cheap exit for instances that never send DMX (e.g. pure output nodes).
            if (sendDMXBuffer.IsEmpty)
                continue;

            try
            {
                var utcNow = DateTime.UtcNow;

                // A frame is due when data changed (rate limited to 44Hz for all universes together)
                // or a universe needs its keep-alive. Nothing is allocated while no frame is due.
                bool anyUpdated = false, anyKeepAlive = false;
                foreach (var bag in sendDMXBuffer.Values)
                {
                    anyUpdated |= bag.Updated;
                    anyKeepAlive |= (utcNow - bag.LastSended).TotalMilliseconds >= dmxKeepAliveTime;
                }
                if (!anyKeepAlive && !(anyUpdated && (utcNow - lastFrame).TotalMilliseconds >= dmxRefreshTime))
                    continue;

                var portsByAddress = getOutputPortsByAddress();
                int sended = 0;
                foreach (var kvp in sendDMXBuffer)
                    try
                    {
                        PortAddress outputPortAddress = kvp.Key;
                        DMXSendBag bag = kvp.Value;
                        if (!bag.Updated && (utcNow - bag.LastSended).TotalMilliseconds < keepAliveBatchTime)
                            continue;

                        // Spec: ArtDmx is only sent to subscribers of the universe.
                        if (!portsByAddress.Contains(outputPortAddress))
                            continue;

                        bag.LastSended = utcNow;
                        // Send only to ports which are inputs into ArtNet
                        PortConfig config = portConfigs?.Where(cfg => cfg.Input)
                                                        .FirstOrDefault(pc => PortAddress.Equals(pc.PortAddress, outputPortAddress));
                        if (config == null)
                            continue;

                        byte sourcePort = config.PortNumber;
                        // One sequence number per frame, shared by every receiver of this frame.
                        var sequence = bag.GetSequence();
#pragma warning disable CS0618 // ForceBroadcast is obsolete for users, but still has to be honoured here
                        bool forceBroadcast = config.ForceBroadcast;
#pragma warning restore CS0618
                        if (forceBroadcast)
                        {
                            // One broadcast reaches all subscribers, so send it once per frame, not once per subscriber.
                            if (portsByAddress[outputPortAddress].Any(port => !port.Timouted()))
                            {
                                sendTasks.Add(sendArtDMXBroadcast(outputPortAddress, sourcePort, bag.Data, sequence));
                                sended++;
                            }
                        }
                        else
                        {
                            sentToIps.Clear();
                            foreach (var port in portsByAddress[outputPortAddress])
                            {
                                if (port.Timouted() || !sentToIps.Add(port.IpAddress))
                                    continue; // one packet per IP, even if it has several matching ports
                                sendTasks.Add(sendArtDMX(port, sourcePort, bag.Data, sequence));
                                sended++;
                            }
                        }

                        foreach (IPv4Address ip in config.AdditionalIPEndpoints.ToList())
                        {
                            sendTasks.Add(sendArtDMX(ip, config.PortAddress, sourcePort, bag.Data, sequence));
                            sended++;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "Send ArtDMX");
                    }

                await Task.WhenAll(sendTasks);
                sendTasks.Clear();

                if (sended != 0)
                {
                    lastFrame = utcNow;
                    // One ArtSync per frame, after all universes of that frame (Spec: ArtSync).
                    if (EnableSync)
                        await sendArtSync();
                }
            }
            catch (Exception ex) { Logger.LogError(ex); }
        }
    }
    #endregion

    private async Task sendArtTodRequest(IPv4Address ipAddress, PortAddress portAddress)
    {

        ArtTodRequest artTodRequest = new ArtTodRequest(portAddress);
        await TrySendPacket(artTodRequest, ipAddress);
    }
    private async Task sendArtTodRequestBroadcast(PortAddress portAddress)
    {

        using ArtTodRequest artTodRequest = new ArtTodRequest(portAddress);
        await TrySendBroadcastPacket(artTodRequest);
    }
    private async Task sendArtTodControl(IPv4Address ipAddress, PortAddress portAddress, EArtTodControlCommand command)
    {

        ArtTodControl artTodControl = new ArtTodControl(portAddress, command);
        await TrySendPacket(artTodControl, ipAddress);
    }
    private async Task sendArtTodControlBroadcast(PortAddress portAddress, EArtTodControlCommand command)
    {

        using ArtTodControl artTodControl = new ArtTodControl(portAddress, command);
        await TrySendBroadcastPacket(artTodControl);
    }

    protected async Task sendArtTodData(IPv4Address ipAddress, PortConfig portConfig)
    {
        ArtTodData artTodData = null;
        List<UID> uids = portConfig.AdditionalRDMUIDs.Select(bag => bag).ToList();
        uids.AddRange(portConfig.DiscoveredRDMUIDs.Select(bag => bag.Uid));
        uids = uids.OrderBy(uid => uid).ToList();
        uids = uids.Distinct().ToList();
        ushort totalCount = (ushort)uids.Count;
        // Spec (ArtTodData): TodFull = "entire ToD or one packet of a sequence that contains the entire ToD",
        // BlockCount 0,1,2,... for blocks of up to 200 UIDs. TodNak means "ToD not available / discovery incomplete",
        // so it is wrong both for the follow-up blocks and for an empty ToD (that is a full ToD with UidTotal 0).
        byte blockCount = 0;
        int offset = 0;
        do
        {
            var current = uids.Skip(offset).Take(ArtTodData.MaxUidsPerPacket).ToArray();
            artTodData = new ArtTodData(portConfig.PortAddress, portConfig.PortNumber, portConfig.BindIndex, totalCount, blockCount, current, EArtTodDataCommandResponse.TodFull);
            await TrySendPacket(artTodData, ipAddress);
            blockCount++;
            offset += current.Length;
        }
        while (offset < uids.Count);
    }
    public async Task SendArtRDM(RDMMessage rdmMessage)
    {
        if (IsDisposed || IsDisposing || IsDeactivated)
            return;

        if (!rdmMessage.Command.HasFlag(ERDM_Command.RESPONSE) && rdmMessage.SourceUID == UID.Empty)
            rdmMessage.SourceUID = UID;

        //if (knownRDMUIDs.TryGetValue(rdmMessage.DestUID, out RDMUID_ReceivedBag uidBag))
        //    rdmMessage.TransactionCounter = uidBag.NewTransactionNumber();

        if (!rdmMessage.Command.HasFlag(ERDM_Command.RESPONSE)) // Response
        {
            var ports = RemoteClientsPorts.Where(port => port.KnownResponderRDMUIDs.Count != 0).Where(port => port.OutputPortAddress.HasValue && port.KnownResponderRDMUIDs.Any(bag => bag.Uid == rdmMessage.DestUID)).ToList();
            List<Task> tasks = new List<Task>();
            List<Tuple<IPv4Address, PortAddress>> tuples = new List<Tuple<IPv4Address, PortAddress>>();
            foreach (var port in ports)
            {
                RDMUID_ReceivedBag bag = port.KnownResponderRDMUIDs.FirstOrDefault(b => b.Uid == rdmMessage.DestUID);
                PortAddress pa = default;
                if (!rdmMessage.Command.HasFlag(ERDM_Command.RESPONSE) && port.OutputPortAddress.HasValue)
                    pa = port.OutputPortAddress.Value;
                else if (rdmMessage.Command.HasFlag(ERDM_Command.RESPONSE) && port.InputPortAddress.HasValue)
                    pa = port.InputPortAddress.Value;
                //Todo Buffer per IP address to prevent Hardware from overflow
                if (tuples.Any(t => t.Item2 == pa && t.Item1 == port.IpAddress))// skip multiple send the same Package to the same IP
                    continue;
                tuples.Add(new Tuple<IPv4Address, PortAddress>(port.IpAddress, pa));
                ArtRDM artRDM = new ArtRDM(bag?.PortAddress ?? pa, rdmMessage);
                tasks.Add(Task.Run(async () =>
                {
                    await handleGatewayQueue(artRDM, port.IpAddress);
                    await TrySendPacket(artRDM, port.IpAddress);
                }));
            }

            await Task.WhenAll(tasks);
        }
    }
    private async Task sendArtRDM(RDMMessage rdmMessage, PortAddress portAddress, IPv4Address ip)
    {
        ArtRDM artRDM = new ArtRDM(portAddress, rdmMessage);
        await handleGatewayQueue(artRDM, ip);
        await TrySendPacket(artRDM, ip);
    }

    //if (artRDMgatewayQueues.TryGetValue(ip, out GatewayRDMFiFoQueue gatewayRDMFiFoQueue))
    //    await gatewayRDMFiFoQueue.AwaitSlot();
    //else
    //{
    //    gatewayRDMFiFoQueue = new GatewayRDMFiFoQueue(ip);
    //    artRDMgatewayQueues.TryAdd(ip, gatewayRDMFiFoQueue);
    //}
    //gatewayRDMFiFoQueue.AddToQueue(artRDM);
    private async Task handleGatewayQueue(ArtRDM artRDM, IPv4Address ip) => await Task.CompletedTask;
    public async Task SendArtNzs(ArtNzs artNzs, IPv4Address ip) => await TrySendPacket(artNzs, ip);

    #region Send ArtAddress
    public async Task SendArtAddress(ArtAddress artAddress, IPv4Address ipAddress) => await sendArtAddress(artAddress, ipAddress);
    private async Task sendArtAddress(ArtAddress artAddress, IPv4Address ipAddress)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        switch (EstCodes)
        {
            case EStCodes.StController:
            case EStCodes.StConfig:
                await TrySendPacket(artAddress, ipAddress);
                return;
        }
    }
    #endregion

    #region Send ArtIpProg
    public async Task SendArtIpProg(ArtIpProg artIpProg, IPv4Address ipAddress) => await sendArtIpProg(artIpProg, ipAddress);
    private async Task sendArtIpProg(ArtIpProg artIpProg, IPv4Address ipAddress)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        switch (EstCodes)
        {
            case EStCodes.StController:
            case EStCodes.StConfig:
                await TrySendPacket(artIpProg, ipAddress);
                return;
        }
    }
    #endregion

    #endregion

    #region Process
    private async Task processArtPollReply(ArtPollReply artPollReply, IPv4Address localIp, IPv4Address sourceIp)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        if (localIp == sourceIp)
        {
            if (MajorVersion == artPollReply.MajorVersion
                && MinorVersion == artPollReply.MinorVersion
                && OEMProductCode == artPollReply.OemCode
                && ESTAManufacturerCode == artPollReply.ManufacturerCode
                //&& IPv4Address.Equals(artPollReply.OwnIp, localIp)
                && EstCodes == artPollReply.Style)
                return; //break loopback
        }

        string id = RemoteClient.getIDOf(artPollReply);
        RemoteClient remoteClient = null;
        await pollReplyProcessSemaphoreSlim.WaitAsync();
        try
        {
            if (remoteClientsTimeouted.TryRemove(id, out remoteClient))
            {
                remoteClient.processArtPollReply(artPollReply);
                add(remoteClient);
            }
            else if (remoteClients.TryGetValue(id, out remoteClient))
            {
                remoteClient.processArtPollReply(artPollReply);
            }
            else
            {
                remoteClient = new RemoteClient(artPollReply) { Instance = this };
                add(remoteClient);
            }

            void add(RemoteClient rc)
            {
                var res = remoteClients.AddOrUpdate(rc.ID, (x) => { return rc; }, (x, y) => { return rc; });
                if (res == rc)
                {
                    Logger.LogInformation($"Discovered: {rc.ID}");
                    Task.Run(() => RemoteClientDiscovered?.InvokeFailSafe(this, rc));
                    return;
                }

                Logger.LogWarning($"Cant add {rc.ID} to Dictionary");
            }
        }
        catch (Exception ex) { Logger.LogError(ex); }

        RemoteClients = remoteClients.Select(p => p.Value).ToList().AsReadOnly();
        outputPortsDirty = true;
        pollReplyProcessSemaphoreSlim.Release();
    }
    private void processArtDMX(ArtDMX artDMX, IPv4Address sourceIp)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        var port = portConfigs.FirstOrDefault(p => p.Type.HasFlag(EPortType.OutputFromArtNet) && p.Universe == artDMX.Address.Universe && p.Subnet == artDMX.Address.Subnet && p.Net == artDMX.Net);
        if (port == null)
            return;

        bool success = false;
        var key = (port.PortAddress, sourceIp, artDMX.Physical);
        if (receivedDMXBuffer.TryGetValue(key, out DMXReceiveBag bag))
            success = bag.Update(artDMX, sourceIp);
        else
            lock (receivedDMXBuffer) // rare path (new source); several NIC receive loops may get here at once
            {
                var utcNow = DateTime.UtcNow;
                var sources = receivedDMXBuffer.Where(kv => kv.Key.Port == port.PortAddress).ToList();
                // Spec: a failed source is held for 10 s, after that it leaves the merge (and makes room for a new source).
                if (sources.Any(kv => utcNow - kv.Value.LastUpdate < MergeSourceTimeout))
                    foreach (var failed in sources.Where(kv => utcNow - kv.Value.LastUpdate >= MergeSourceTimeout))
                        if (receivedDMXBuffer.TryRemove(failed.Key, out var removed))
                            removed.Dispose();

                // Spec: merging is limited to two sources, any additional sources are ignored.
                if (getMergeSources(port.PortAddress, utcNow).Count >= MaxMergeSources)
                    return;

                if (receivedDMXBuffer.TryGetValue(key, out bag))
                    success = bag.Update(artDMX, sourceIp);
                else
                {
                    bag = new DMXReceiveBag(artDMX, sourceIp);
                    success = receivedDMXBuffer.TryAdd(key, bag);
                }
            }

        if (success)
        {
            // Spec: GoodOutput bit 3 is set while this output is merging.
            bool merging = getMergeSources(port.PortAddress, DateTime.UtcNow).Count > 1;
            var goodOutput = port.GoodOutput | GoodOutput.DATA_TRANSMITTED;
            port.GoodOutput = merging ? goodOutput | MERGING : goodOutput & ~MERGING;
            DMXReceived?.InvokeFailSafe(this, port.PortAddress);
        }
    }
    protected async Task processArtData(ArtData artData, IPv4Address source)
    {
        if (IsDisposing || IsDisposed || IsDeactivated || !SendArtData)
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
                case EDataRequest.UrlProduct:
                    str = UrlProduct;
                    break;
                case EDataRequest.UrlUserGuide:
                    str = UrlUserGuid;
                    break;
                case EDataRequest.UrlSupport:
                    str = UrlSupport;
                    break;
                case EDataRequest.UrlPersUdr:
                    str = UrlPersonalityUDR;
                    break;
                case EDataRequest.UrlPersGdtf:
                    str = UrlPersonalityGDTF;
                    break;
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

    protected virtual ArtDataReply buildArtDataReply(ArtData artData) => new ArtDataReply(OEMProductCode, ESTAManufacturerCode, artData.Request, data: null);

    protected async Task processArtTodRequest(ArtTodRequest artTodRequest, IPv4Address source)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        try
        {
            List<PortAddress> portAddresses = new List<PortAddress>();
            var configs = PortConfigs.Where(p => p.Output && artTodRequest.PortAddresses.Contains(p.PortAddress)).ToList();

            List<Task> tasks = new List<Task>();
            foreach (var p in configs)
                tasks.Add(Task.Run(async () =>
                {
                    await sendArtTodData(source, p);
                }));

            await Task.WhenAll(tasks);
        }
        catch (Exception ex) { Logger.LogError(ex); }
    }
    protected async Task processArtTodControl(ArtTodControl artTodControl, IPv4Address source)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        try
        {
            var configs = PortConfigs.Where(p => p.Output && PortAddress.Equals(p.PortAddress, artTodControl.PortAddress)).ToList();
            //if (artTodControl.Command == EArtTodControlCommand.AtcFlush)
            //    continue;// ToDo

            List<Task> tasks = new List<Task>();
            foreach (var p in configs)
                tasks.Add(Task.Run(async () =>
                {
                    await sendArtTodData(source, p);
                    await PerformRDMDiscoverOnOutput(p);
                    await sendArtTodData(source, p);
                }));

            await Task.WhenAll(tasks);
        }
        catch (Exception ex) { Logger.LogError(ex); }
    }
    protected void processArtTodData(ArtTodData artTodData, IPv4Address source)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;

        // Spec: TodNak = ToD not available / discovery incomplete -> keep what is known.
        if (artTodData.Command == EArtTodDataCommandResponse.TodNak)
            return;

        // A TodFull with UidTotal 0 is a valid, empty ToD: all responders on this Port-Address are gone.
        bool emptyTod = artTodData.UidTotalCount == 0;
        if (!emptyTod && artTodData.Uids.Length == 0)
            return;

        byte bindIndex = artTodData.BindIndex;
        byte port = artTodData.Port;

        try
        {
            if (RemoteClientsPorts.Count != 0)
            {
                var ports = RemoteClientsPorts
                    .Where(p => IPv4Address.Equals(p.IpAddress, source) && p.BindIndex == bindIndex)
                    .ToList();

                if (ports.Count == 0)
                {
                    Logger?.LogWarning($"No Port found for Port: {port}, BindIndex: {bindIndex}, SourceIp: {source}");
                    ports = RemoteClientsPorts
                    .Where(p => IPv4Address.Equals(p.IpAddress, source) && p.BindIndex == port)
                    .ToList();
                    foreach (var _port in ports)
                        if (emptyTod)
                            _port.ClearResponderRdmUIDs(artTodData.PortAddress);
                        else
                            _port.AddResponderRdmUIDs(artTodData.PortAddress, port, artTodData.Uids);
                }
                else
                {
                    foreach (var _port in ports)
                        if (emptyTod)
                            _port.ClearResponderRdmUIDs(artTodData.PortAddress);
                        else
                            _port.AddResponderRdmUIDs(artTodData.PortAddress, bindIndex, artTodData.Uids);
                }
            }
        }
        catch (Exception ex) { Logger.LogError(ex); }

        try
        {
            var configs = portConfigs
                .Where(p => p.Input && p.BindIndex == artTodData.BindIndex && PortAddress.Equals(p.PortAddress, artTodData.PortAddress))
                .ToList();

            foreach (var config in configs)
                if (emptyTod)
                    config.ClearDiscoveredRdmUIDs(artTodData.PortAddress);
                else
                    config.AddDiscoveredRdmUIDs(artTodData.PortAddress, artTodData.BindIndex, artTodData.Uids);
        }
        catch (Exception ex) { Logger.LogError(ex); }

        try
        {
            if (emptyTod)
                clearRdmUIDs(artTodData.PortAddress, artTodData.BindIndex);
            else
                AddRdmUIDs(artTodData.PortAddress, artTodData.BindIndex, artTodData.Uids);
        }
        catch (Exception ex) { Logger.LogError(ex); }
    }
    private async void processArtRDM(ArtRDM artRDM, IPv4Address source)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;
        ControllerRDMUID_Bag bag = null;
        try
        {
            if (artRDMgatewayQueues.TryGetValue(source, out GatewayRDMFiFoQueue gatewayRDMFiFoQueue))
            {
                gatewayRDMFiFoQueue.Update(artRDM);
            }

            if (!artRDM.RDMMessage.Command.HasFlag(ERDM_Command.RESPONSE))
            {
                if (knownControllerRDMUIDs.TryGetValue(artRDM.Source, out bag))
                    bag.Seen();
                else
                {
                    bag = new ControllerRDMUID_Bag(artRDM.Source, artRDM.PortAddress, source);
                    knownControllerRDMUIDs.TryAdd(artRDM.Source, bag);
                }
            }
            else if (knownControllerRDMUIDs.TryGetValue(artRDM.Destination, out bag))
                return;

            var ports = RemoteClientsPorts
                .Where(p => IPv4Address.Equals(p.IpAddress, source) && (artRDM.RDMMessage.Command.HasFlag(ERDM_Command.RESPONSE) ? PortAddress.Equals(p.InputPortAddress, artRDM.PortAddress) : PortAddress.Equals(p.OutputPortAddress, artRDM.PortAddress)))
                .ToList();
            if (ports.Count != 0)
            {
                foreach (var port in ports)
                    port.ProcessArtRDM(artRDM);
            }
        }
        catch (Exception ex) { Logger.LogError(ex); }

        var ti = new RDM_TransactionID(artRDM.RDMMessage.TransactionCounter, artRDM.Source, artRDM.Destination);
        if (!artRDMdeBumbReceive.TryAdd(ti, artRDM.RDMMessage))
            return;
        try
        {
            if (!artRDM.RDMMessage.Command.HasFlag(ERDM_Command.RESPONSE))
            {
                var eventArgs = new RequestRDMMessageReceivedEventArgs(artRDM.RDMMessage, artRDM.PortAddress);
                RequestRDMMessageReceived?.InvokeFailSafe(this, eventArgs);
                if (eventArgs.Handled)
                    await sendArtRDM(eventArgs.Response, artRDM.PortAddress, source);
            }
            else
            {
                var eventArgs = new ResponseRDMMessageReceivedEventArgs(artRDM.RDMMessage, artRDM.PortAddress);

                ResponseRDMMessageReceived?.InvokeFailSafe(this, eventArgs);
            }
        }
        catch (Exception ex) { Logger.LogError(ex); }

        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            artRDMdeBumbReceive.TryRemove(ti, out _);
        });
    }
    #endregion

    public void AddPortConfig(params PortConfig[] portConfigs)
    {
        if (IsDisposing || IsDisposed)
            return;

        lock (portConfigsWriteLock)
        {
            var list = new List<PortConfig>(this.portConfigs);
            foreach (var portConfig in portConfigs)
            {
                list.Add(portConfig);
                Logger?.LogDebug($"Added PortConfig {portConfig}");
            }
            this.portConfigs = list.ToArray(); // copy-on-write: readers keep their consistent snapshot
        }
    }
    public void RemovePortConfig(params PortConfig[] portConfigs)
    {
        if (IsDisposing || IsDisposed)
            return;

        lock (portConfigsWriteLock)
        {
            var list = new List<PortConfig>(this.portConfigs);
            foreach (var portConfig in portConfigs)
            {
                list.Remove(portConfig);
                Logger?.LogDebug($"Removed PortConfig {portConfig}");
            }
            this.portConfigs = list.ToArray(); // copy-on-write: readers keep their consistent snapshot
        }
    }

    public void WriteDMXValues(PortAddress portAddress, byte[] data, ushort? destinationIndex = null, ushort? count = null)
    {
        if (IsDisposing || IsDisposed)
            return;

        if (data.Length > 512)
            throw new ArgumentOutOfRangeException();

        ushort length = (ushort)data.Length;
        if (count is null)
            count = length;

        if (count > length)
            throw new ArgumentOutOfRangeException($"{nameof(count)} has to be less then {nameof(data)}.{nameof(data.Length)}");

        if (destinationIndex + count > 512)
            throw new ArgumentOutOfRangeException($"{nameof(destinationIndex)} + {nameof(count)} has to be less then 512");

        int _startIndex = 0;
        int _count = length;
        if (destinationIndex.HasValue)
            _startIndex = destinationIndex.Value;
        if (count.HasValue)
            _count = Math.Min(_count, count.Value);

        try
        {
            // GetOrAdd: the first write for a Port-Address goes through Update as well (so destinationIndex/count
            // are honoured), and two concurrent first writes end up in the same buffer instead of one getting lost.
            DMXSendBag bag = sendDMXBuffer.GetOrAdd(portAddress, pa => new DMXSendBag(pa));
            bag.Update(data, destinationIndex, count);
        }
        catch (Exception e) { Logger.LogError(e); }
    }

    public byte[] GetReceivedDMX(PortAddress portAddress, EMergeMode mergeMode = EMergeMode.HTP)
    {
        if (IsDisposing || IsDisposed || receivedDMXBuffer.IsEmpty)
            return null;

        // Spec (Data Merging): only sources that did not fail (10 s) are merged; if all failed, the last merge result is held.
        var bags = getMergeSources(portAddress, DateTime.UtcNow);

        if (bags.Count == 0)
            return null;

        if (bags.Count == 1)
            return bags[0].Data;

        switch (mergeMode)
        {
            case EMergeMode.HTP:
                int length = bags.Select(b => b.Data.Length).OrderBy(b => b).Last();
                byte[] dataResult = new byte[length];
                for (ushort i = 0; i < dataResult.Length; i++)
                {
                    byte val = 0;
                    bags.ForEach(b => { if (i < b.Data.Length) val = Math.Max(val, b.Data[i]); });
                    dataResult[i] = val;
                }

                return dataResult;
            case EMergeMode.LTP:
                var bag = bags.OrderBy(b => b.LastUpdate.Ticks).LastOrDefault();
                return bag?.Data;
        }

        return null;
    }
    public UID[] GetReceivedRDMUIDs()
    {
        if (IsDisposing || IsDisposed)
            return null;

        return KnownRDMUIDs.Where(k => !k.Timouted()).Select(k => k.Uid).ToArray();
    }

    protected async Task PerformRDMDiscovery(PortAddress? portAddress = null, bool flush = false, bool broadcast = false)
    {
        if (IsDisposing || IsDisposed)
            return;

        List<RemoteClientPort> ports = null;
        if (!portAddress.HasValue)
            ports = RemoteClientsPorts.Where(port => port.OutputPortAddress.HasValue).ToList();
        else
            ports = RemoteClientsPorts.Where(port => port.OutputPortAddress.HasValue && PortAddress.Equals(port.OutputPortAddress.Value, portAddress.Value)).ToList();

        List<Task> tasks = new List<Task>();
        foreach (var port in ports)
        {
            if (!port.IsRDMCapable)
                continue;

            tasks.Add(Task.Run(async () =>
            {
                if (broadcast)
                {
                    if (flush)
                        await sendArtTodControlBroadcast(port.OutputPortAddress.Value, EArtTodControlCommand.AtcFlush);
                    else
                        await sendArtTodRequestBroadcast(port.OutputPortAddress.Value);
                }
                else
                {
                    if (flush)
                        await sendArtTodControl(port.IpAddress, port.OutputPortAddress.Value, EArtTodControlCommand.AtcFlush);
                    else
                        await sendArtTodRequest(port.IpAddress, port.OutputPortAddress.Value);
                }
            }));
        }

        await Task.WhenAll(tasks);
    }

    protected virtual async Task PerformRDMDiscoverOnOutput(PortConfig portConfig) => await Task.Delay(500);

    protected virtual NodeStatus GetOwnNodeStatus() => NodeStatus.None;
    private NodeStatus getOwnNodeStatus()
    {
        NodeStatus nodeStatus = GetOwnNodeStatus() | NodeStatus.NodeSupports15BitPortAddress;

        if (SupportRDM)
            nodeStatus |= NodeStatus.RDMSupported;

        return nodeStatus;
    }
    private void clearRdmUIDs(PortAddress portAddress, byte bindIndex)
    {
        bool removed = false;
        foreach (var entry in knownRDMUIDs.Where(kv => kv.Value.PortAddress == portAddress && kv.Value.BindIndex == bindIndex).ToList())
            removed |= knownRDMUIDs.TryRemove(entry.Key, out _);
        if (removed)
            KnownRDMUIDs = knownRDMUIDs.Values.ToList().AsReadOnly();
    }
    private void AddRdmUIDs(PortAddress portAddress, byte bindIndex, params UID[] rdmuids)
    {
        if (rdmuids.Length == 0)
            return;

        foreach (UID rdmuid in rdmuids)
        {
            if (knownRDMUIDs.TryGetValue(rdmuid, out RDMUID_ReceivedBag bag))
                bag.Seen(portAddress, bindIndex);
            else
            {
                bag = new RDMUID_ReceivedBag(portAddress, bindIndex, rdmuid);
                if (knownRDMUIDs.TryAdd(rdmuid, bag))
                    RDMUIDReceived?.InvokeFailSafe(this, bag);
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

    private async Task triggerSendArtPoll()
    {
        if (IsDisposed || IsDisposing || IsDeactivated)
            return;
        //if (EstCodes != EStCodes.StController)// As Spec. only Controler are allowed to send ArtPoll
        //    return;
        if (SendArtPollBroadcast)
            await sendArtPoll();
        else if (SendArtPollTargeted)
        {
            var ports = portConfigs.Where(p => p.Type.HasFlag(EPortType.InputToArtNet)).OrderBy(p => p.PortAddress).ToList();
            PortAddress? bottom = null;
            PortAddress? top = null;

            for (int i = 0; i < ports.Count; i++)
            {
                PortConfig port = ports[i];
                PortConfig nextPort = null;
                if (i + 1 < ports.Count)
                    nextPort = ports[i + 1];

                if (!bottom.HasValue)
                    bottom = port.PortAddress;

                if (port.PortAddress.Combined + 1 == nextPort?.PortAddress.Combined)
                    continue;
                else
                    top = port.PortAddress;

                if (bottom.HasValue && top.HasValue)
                {
                    await sendArtPoll(top.Value, bottom.Value);
                    bottom = null;
                    top = null;
                }
            }
        }
    }

    private async void TimerSendPoll_Elapsed(object sender, EventArgs e)
    {
        if (IsDeactivated)
            return;

        await triggerSendArtPoll();
        await Task.Delay(4000);
        await pollReplyProcessSemaphoreSlim.WaitAsync();
        try
        {
            var timoutedClients = remoteClients.Where(p => p.Value.Timouted()).ToList().AsReadOnly();
            if (timoutedClients.Count() != 0)
            {
                foreach (var rc in timoutedClients)
                {

                    if (remoteClients.TryRemove(rc.Key, out RemoteClient removed))
                        remoteClientsTimeouted.TryAdd(removed.ID, removed);
                    else
                        Logger.LogWarning($"Can't remove RemoteClient({removed.ID}) from ConcurrentDictionary");

                    if (removed != null)
                    {
                        Logger.LogInformation($"Timeout: {removed.ID} ({(DateTime.UtcNow - rc.Value.LastSeen).TotalMilliseconds}ms)");
                        _ = Task.Run(() => RemoteClientTimedOut?.InvokeFailSafe(this, removed));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }

        RemoteClients = remoteClients.Select(p => p.Value).ToList().AsReadOnly();
        outputPortsDirty = true;
        pollReplyProcessSemaphoreSlim.Release();
    }

    void IDisposable.Dispose()
    {
        if (IsDisposed || IsDisposing)
            return;

        Logger.LogInformation($"Disposing {Name}");
        IsDisposing = true;
        try
        {

            if (!IsDeactivated)
                ArtNetInstance.RemoveInstance(this);

            ArtNetInstance.OnInstanceAdded -= ArtNet_OnInstanceAdded;
            ArtNetInstance.OnInstanceRemoved -= ArtNet_OnInstanceRemoved;
            // The poll event is static: without this the disposed instance would stay referenced forever.
            sendPollThreadBag.SendArtPollEvent -= TimerSendPoll_Elapsed;

            foreach (var rBuffer in receivedDMXBuffer.Values)
                rBuffer.Dispose();
            receivedDMXBuffer.Clear();

            foreach (var sBuffer in sendDMXBuffer.Values)
                sBuffer.Dispose();
            sendDMXBuffer.Clear();

            RemovePortConfig(portConfigs.ToArray());

            portConfigs = Array.Empty<PortConfig>();
            remoteClients.Clear();
            knownControllerRDMUIDs.Clear();
            knownRDMUIDs.Clear();
            RemoteClients = null;

            Dispose();
        }
        catch (Exception e) { Logger.LogError(e); }
        finally
        {
            ArtNetInstance = null;
            IsDisposed = true;
            IsDisposing = false;
            Logger.LogInformation($"Disposed {Name}");
            GC.SuppressFinalize(this);
        }
    }

    protected virtual void Dispose()
    {

    }

    public class GatewayRDMFiFoQueue : INotifyPropertyChanged
    {
        public readonly IPv4Address IpAddress;

        private readonly List<ArtRDM> bag = new List<ArtRDM>();

        public int Available
        {
            get;
            private set
            {
                if (field == value)
                    return;

                field = value;
                PropertyChanged?.InvokeFailSafe(this, new PropertyChangedEventArgs(nameof(Available)));
            }
        } = 0;
        public byte Max { get; private set; } = 0;
        public bool Implemented { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public GatewayRDMFiFoQueue(IPv4Address ipAddress)
        {
            IpAddress = ipAddress;
        }

        internal void Update(ArtRDM response)
        {
            if (!response.RDMMessage.Command.HasFlag(ERDM_Command.RESPONSE))
                return;

            Max = response.FifoMax;
            Implemented = Max != 0;
            if (Implemented)
            {
                Available = response.FifoAvailable;
            }
            else
            {
                if (bag.FirstOrDefault(b => b.PortAddress == response.PortAddress && b.RDMMessage.DestUID == response.RDMMessage.SourceUID) is ArtRDM request)
                {
                    bag.Remove(request);
                    Available = 1;
                }
            }
        }

        internal void AddToQueue(ArtRDM request)
        {
            bag.Add(request);
            Available--;

            Task.Run(async () =>
            {
                await Task.Delay(10000);
                bag.Remove(request);
                Available++;
            });
        }
        internal async Task AwaitSlot()
        {
            Task.Run(async () =>
            {
                while (Available <= 0)
                    await Task.Delay(30);
            }).Wait(3000);
        }
    }
}