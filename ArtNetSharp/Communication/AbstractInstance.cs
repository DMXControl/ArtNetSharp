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
    /// <summary>
    /// Logger of this instance, categorised by its concrete type. It used to be static and was overwritten
    /// by every new instance, so all instances logged under the type of the last one created.
    /// </summary>
    protected ILogger Logger { get; }
    /// <summary>For the static/nested parts (e.g. the shared poll thread) that have no instance.</summary>
    private static readonly ILogger StaticLogger = Logging.CreateLogger<AbstractInstance>();
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
    private readonly ConcurrentDictionary<RDM_TransactionID, DateTime> artRDMdeBumbReceive = new();

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
        private readonly object syncRoot = new object();

        /// <summary>Copies the current frame into <paramref name="target"/> (consistent snapshot).</summary>
        internal void CopyTo(byte[] target)
        {
            lock (syncRoot)
            {
                var data = Data;
                if (data != null)
                    Array.Copy(data, 0, target, 0, Math.Min(data.Length, target.Length));
            }
        }
        /// <summary>HTP: target[i] = max(target[i], frame[i]) (consistent snapshot).</summary>
        internal void MergeHtpInto(byte[] target)
        {
            lock (syncRoot)
            {
                var data = Data;
                if (data == null)
                    return;
                int length = Math.Min(data.Length, target.Length);
                for (int i = 0; i < length; i++)
                    if (data[i] > target[i])
                        target[i] = data[i];
            }
        }

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

            lock (syncRoot) // readers copy under the same lock, so they never see half a frame
            {
                var data = Data;
                if (data == null)
                    return false;
                Array.Copy(artDMX.Data, 0, data, 0, Math.Min(artDMX.Data.Length, data.Length));
            }

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
        /// <summary>Last time ArtDmx for this port actually went out (to at least one receiver, keep-alives included).</summary>
        public DateTime LastTransmitted { get; internal set; } = DateTime.MinValue;

        // Writes (WriteDMXValues) and the send loop's snapshot use the same lock:
        // writes are applied in call order and a frame is never sent half-written.
        private readonly object syncRoot = new object();

        public bool IsDisposed;
        public bool IsDisposing;

        /// <summary>Creates an empty (all zero) buffer; data is written via <see cref="Update"/>.</summary>
        public DMXSendBag(PortAddress portAddress)
        {
            PortAddress = portAddress;
        }

        internal void Update(byte[] data, ushort? destinationIndex = null, ushort? count = null)
        {
            if (IsDisposed || IsDisposing)
                return;

            lock (syncRoot)
            {
                var buffer = Data;
                if (buffer == null)
                    return;
                // Copy data[0..count) to Data[destinationIndex..), clipped to the 512 channels.
                int start = destinationIndex ?? 0;
                int length = Math.Min(Math.Min(count ?? data.Length, data.Length), buffer.Length - start);
                if (length > 0)
                    Array.Copy(data, 0, buffer, start, length);

                LastUpdated = DateTime.UtcNow;
            }
        }

        /// <summary>A consistent copy of the current frame for sending.</summary>
        internal byte[] Snapshot()
        {
            lock (syncRoot)
                return (byte[])Data?.Clone() ?? Array.Empty<byte>();
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
            lock (syncRoot)
                Data = null;
            Sequence = 0;
            LastUpdated = DateTime.MaxValue;
            LastSended = DateTime.MaxValue;
            IsDisposed = true;
            IsDisposing = false;
            GC.SuppressFinalize(this);
        }
    }
    private readonly ConcurrentDictionary<PortAddress, DMXSendBag> sendDMXBuffer = new ConcurrentDictionary<PortAddress, DMXSendBag>();

    /// <summary>
    /// Raises the events of this instance (and of its RemoteClients, ports and port configs) ordered and
    /// fail-safe outside the receive loops, so application handlers can neither block nor break the library.
    /// </summary>
    internal EventDispatcher Events { get; }

    /// <summary>
    /// DMXReceived is coalesced per Port-Address: at most one event per port is pending. With a slow handler the
    /// queue therefore cannot grow with the packet rate; the handler reads the latest data via GetReceivedDMX.
    /// </summary>
    private readonly ConcurrentDictionary<PortAddress, byte> dmxReceivedPending = new ConcurrentDictionary<PortAddress, byte>();
    /// <summary>Longer than the ArtDmx keep-alive interval (800-1000 ms), so a running data flow never drops out.</summary>
    internal static TimeSpan TransmittingTimeout { get; set; } = TimeSpan.FromSeconds(2.5); // settable for tests only
    private bool isTransmitting(PortAddress portAddress)
        => sendDMXBuffer.TryGetValue(portAddress, out DMXSendBag bag)
           && DateTime.UtcNow - bag.LastTransmitted < TransmittingTimeout;

    private void raiseDMXReceived(PortAddress portAddress)
    {
        if (!dmxReceivedPending.TryAdd(portAddress, 0))
            return; // an event for this port is already queued and will see the new data
        Events.Post(() =>
        {
            dmxReceivedPending.TryRemove(portAddress, out _); // before the handler: data arriving meanwhile queues a new event
            DMXReceived?.InvokeFailSafe(this, portAddress);
        });
    }
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
                        StaticLogger.LogError(ex);
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
        Events = new EventDispatcher(GetType().Name, Logger);

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

        try // async void: an escaping exception would terminate the host application
        {
            await triggerSendArtPoll();
        }
        catch (Exception ex) { Logger.LogError(ex); }
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
                    registerReplyOnChange(artPoll, localIp, sourceIp);
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
                    Events.Post(() => SyncReceived?.InvokeFailSafe(this, EventArgs.Empty));
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
                    Events.Post(() => TimeCodeReceived?.InvokeFailSafe(this, artTimeCode));
                    break;
                case ArtTimeSync artTimeSync:
                    Events.Post(() => TimeSyncReceived?.InvokeFailSafe(this, artTimeSync));
                    break;

                case ArtAddress artAddress when AcceptArtAddress:
                    _ = processArtAddress(artAddress, localIp, sourceIp);
                    break;

                case ArtIpProg artIpProg when AcceptArtAddress: // same receivers as ArtAddress: Nodes and Media Servers
                    _ = processArtIpProg(artIpProg, localIp, sourceIp);
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
            // Spec: the random delay (up to 1 s) applies to replies to an ArtPoll. Replies without a poll
            // (to an ArtAddress, or because node conditions changed) are sent right away.
            if (artPoll != null)
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
            if (artPoll?.Flags.HasFlag(EArtPollFlags.EnableTargetedMode) ?? false)
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
                        // GoodInput bit 7 "Data received": only while ArtDmx for this input port is actually being sent.
                        // Keep-alives go out every 800-1000 ms, so a longer gap means the data flow stopped
                        // (no subscribers any more, instance deactivated, ...). Previously the bit stayed set forever
                        // once anything had been written for the port.
                        if (portConfig.Input && isTransmitting(portConfig.PortAddress))
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
            checkNodeConditionsChanged(); // cheap no-op unless a controller asked for "ArtPollReply on change"
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
                        // One sequence number and one consistent snapshot per frame, shared by every receiver of this frame.
                        var sequence = bag.GetSequence();
                        byte[] frame = bag.Snapshot();
                        int sendedBefore = sended;
#pragma warning disable CS0618 // ForceBroadcast is obsolete for users, but still has to be honoured here
                        bool forceBroadcast = config.ForceBroadcast;
#pragma warning restore CS0618
                        if (forceBroadcast)
                        {
                            // One broadcast reaches all subscribers, so send it once per frame, not once per subscriber.
                            if (portsByAddress[outputPortAddress].Any(port => !port.Timouted()))
                            {
                                sendTasks.Add(sendArtDMXBroadcast(outputPortAddress, sourcePort, frame, sequence));
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
                                sendTasks.Add(sendArtDMX(port, sourcePort, frame, sequence));
                                sended++;
                            }
                        }

                        foreach (IPv4Address ip in config.AdditionalIPEndpoints.ToList())
                        {
                            sendTasks.Add(sendArtDMX(ip, config.PortAddress, sourcePort, frame, sequence));
                            sended++;
                        }

                        if (sended != sendedBefore)
                            bag.LastTransmitted = utcNow; // feeds GoodInput "Data received" in the ArtPollReply
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

    /// <summary>Waits for a free entry in the gateway's RDM transmit queue (only if the gateway implements one).</summary>
    private async Task handleGatewayQueue(ArtRDM artRDM, IPv4Address ip)
    {
        if (artRDM.RDMMessage.Command.HasFlag(ERDM_Command.RESPONSE))
            return; // the queue limits requests sent to a gateway, not our own responses
        await getGatewayQueue(ip).AcquireSlotAsync();
    }
    private GatewayRDMFiFoQueue getGatewayQueue(IPv4Address ip)
        => artRDMgatewayQueues.GetOrAdd(ip, address => new GatewayRDMFiFoQueue(address, Events, Logger));

    /// <summary>The RDM transmit queue state of a gateway (Art-Net 4 FifoAvail / FifoMax), or null if it never sent ArtRdm.</summary>
    public GatewayRDMFiFoQueue GetGatewayRdmQueue(IPv4Address ip)
        => artRDMgatewayQueues.TryGetValue(ip, out var queue) ? queue : null;
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
                remoteClient = new RemoteClient(artPollReply, Events) { Instance = this };
                add(remoteClient);
            }

            void add(RemoteClient rc)
            {
                var res = remoteClients.AddOrUpdate(rc.ID, (x) => { return rc; }, (x, y) => { return rc; });
                if (res == rc)
                {
                    Logger.LogInformation($"Discovered: {rc.ID}");
                    Events.Post(() => RemoteClientDiscovered?.InvokeFailSafe(this, rc));
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

        if (!cancelMergePending.IsEmpty || !exclusiveSource.IsEmpty) // AcCancelMerge, see applyArtAddress
        {
            if (cancelMergePending.TryRemove(port.PortAddress, out _))
            {
                // Spec: this ArtDmx ends merge mode; only its source is accepted from now on.
                exclusiveSource[port.PortAddress] = (sourceIp, artDMX.Physical);
                lock (receivedDMXBuffer)
                    foreach (var other in receivedDMXBuffer.Where(kv => kv.Key.Port == port.PortAddress && kv.Key != key).ToList())
                        if (receivedDMXBuffer.TryRemove(other.Key, out var removed))
                            removed.Dispose();
            }
            else if (exclusiveSource.TryGetValue(port.PortAddress, out var exclusive) && exclusive != (sourceIp, artDMX.Physical))
            {
                bool exclusiveAlive = receivedDMXBuffer.TryGetValue((port.PortAddress, exclusive.Ip, exclusive.Physical), out var exclusiveBag)
                                      && DateTime.UtcNow - exclusiveBag.LastUpdate < MergeSourceTimeout;
                if (exclusiveAlive)
                    return; // discard other sources while the remaining one is alive
                exclusiveSource.TryRemove(port.PortAddress, out _); // it failed: normal operation again
            }
        }

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
            raiseDMXReceived(port.PortAddress);
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

    #region ArtAddress
    /// <summary>
    /// Whether this instance can be reprogrammed remotely via ArtAddress. Spec: Nodes and Media Servers act on it,
    /// Controllers take no action. Override and return false to ignore ArtAddress completely;
    /// to decide per packet, handle <see cref="ArtAddressReceived"/> and set Cancel.
    /// </summary>
    protected virtual bool AcceptArtAddress => EstCodes == EStCodes.StNode || EstCodes == EStCodes.StMedia;

    /// <summary>Raised before an ArtAddress is applied; set <see cref="ArtAddressReceivedEventArgs.Cancel"/> to reject it.</summary>
    public event EventHandler<ArtAddressReceivedEventArgs> ArtAddressReceived;

    /// <summary>Ports in merge mode for which the next ArtDmx ends merging (AcCancelMerge).</summary>
    private readonly ConcurrentDictionary<PortAddress, byte> cancelMergePending = new ConcurrentDictionary<PortAddress, byte>();
    /// <summary>After AcCancelMerge: the only source accepted for a port, until it fails (MergeSourceTimeout).</summary>
    private readonly ConcurrentDictionary<PortAddress, (IPv4Address Ip, byte Physical)> exclusiveSource = new ConcurrentDictionary<PortAddress, (IPv4Address Ip, byte Physical)>();

    private async Task processArtAddress(ArtAddress artAddress, IPv4Address localIp, IPv4Address source)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;
        try
        {
            var args = new ArtAddressReceivedEventArgs(artAddress, source);
            // Veto and apply in one step on the event queue (not on the receive loop): this keeps several
            // ArtAddress packets strictly ordered - applying after the await let a later packet overtake an earlier one.
            await Events.InvokeAsync(() =>
            {
                ArtAddressReceived?.InvokeFailSafe(this, args);
                if (!args.Cancel)
                    applyArtAddress(artAddress);
                return args;
            });

            // Spec: the node replies to an ArtAddress by unicasting ArtPollReply (also when nothing was changed).
            await sendArtPollReply(localIp, source);
        }
        catch (Exception ex) { Logger.LogError(ex); }
    }

    private void applyArtAddress(ArtAddress artAddress)
    {
        // Names: the spec says null strings are ignored.
        if (!string.IsNullOrEmpty(artAddress.ShortName))
            ShortName = artAddress.ShortName.Length > 17 ? artAddress.ShortName.Substring(0, 17) : artAddress.ShortName;
        if (!string.IsNullOrEmpty(artAddress.LongName))
            Name = artAddress.LongName.Length > 63 ? artAddress.LongName.Substring(0, 63) : artAddress.LongName;

        // Ports of the addressed bound device (BindIndex 0 and 1 both mean the root device),
        // in port order; the index into SwIn/SwOut is the position within that BindIndex.
        byte bindIndex = Math.Max((byte)1, artAddress.BindIndex);
        var ports = portConfigs.Where(pc => pc.BindIndex == bindIndex).OrderBy(pc => pc.PortNumber).ToList();
        for (int i = 0; i < ports.Count && i < 4; i++)
        {
            PortConfig port = ports[i];
            PortAddress current = port.PortAddress;
            PortAddress configured = port.ConfiguredPortAddress ?? current;

            Net net = artAddress.Net ?? (artAddress.NetReset ? configured.Net : current.Net);
            Subnet subnet = artAddress.Subnet ?? (artAddress.SubnetReset ? configured.Subnet : current.Subnet);

            // An output port takes SwOut, an input port SwIn; a port with both directions takes whichever is given.
            Universe? programmed = null;
            bool reset = false;
            if (port.Output)
            {
                programmed = artAddress.OutputUniverses[i];
                reset = artAddress.OutputUniverseReset[i];
            }
            if (port.Input && !programmed.HasValue && !reset)
            {
                programmed = artAddress.InputUniverses[i];
                reset = artAddress.InputUniverseReset[i];
            }
            Universe universe = programmed ?? (reset ? configured.Universe : current.Universe);

            var newAddress = new PortAddress(net, subnet, universe);
            if (newAddress != current)
            {
                port.ConfiguredPortAddress ??= current;
                port.PortAddress = newAddress;
                Logger.LogInformation($"ArtAddress: Port #{port.BindIndex}/{port.PortNumber} Port-Address {current} -> {newAddress}");
            }
        }

        ArtAddressCommand command = artAddress.Command;
        switch (command.Command)
        {
            case EArtAddressCommand.MergeLtp:
            case EArtAddressCommand.MergeHtp:
                // x = port index within this BindIndex
                if (command.Port is byte x && x < ports.Count)
                {
                    var port = ports[x];
                    var withoutLtp = port.GoodOutput & ~MERGE_LTP;
                    port.GoodOutput = command.Command == EArtAddressCommand.MergeLtp ? withoutLtp | MERGE_LTP : withoutLtp;
                }
                break;

            case EArtAddressCommand.CancelMerge:
                // Spec: the next ArtDmx ends merge mode; afterwards only that source is accepted.
                foreach (var port in portConfigs.Where(pc => pc.Output))
                    if (getMergeSources(port.PortAddress, DateTime.UtcNow).Count > 1)
                        cancelMergePending[port.PortAddress] = 0;
                break;
        }
    }

    private static readonly GoodOutput MERGE_LTP = new GoodOutput(mergeMode: EMergeMode.LTP);
    #endregion

    #region ArtPollReply on change
    /// <summary>
    /// Controllers that set ArtPoll Flags bit 1 ("Send ArtPollReply whenever Node conditions change"), with the time of
    /// their last ArtPoll and the local address they reached us on. They get an unsolicited ArtPollReply on every change.
    /// </summary>
    private readonly ConcurrentDictionary<IPv4Address, (DateTime LastPoll, IPv4Address LocalIp)> replyOnChangeSubscribers = new();
    /// <summary>A controller that stops polling (2.5-3 s per spec) is no longer notified after this time.</summary>
    internal static TimeSpan ReplyOnChangeSubscriptionTimeout { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>Conditions are compared at most this often (so a burst of changes leads to one reply).</summary>
    internal static TimeSpan NodeConditionsCheckInterval { get; set; } = TimeSpan.FromMilliseconds(250);
    private DateTime lastNodeConditionsCheck = DateTime.MinValue;
    private int? lastNodeConditions;

    private void registerReplyOnChange(ArtPoll artPoll, IPv4Address localIp, IPv4Address sourceIp)
    {
        if (artPoll.Flags.HasFlag(EArtPollFlags.ArtPollReplyOnChange))
            replyOnChangeSubscribers[sourceIp] = (DateTime.UtcNow, localIp);
        else
            replyOnChangeSubscribers.TryRemove(sourceIp, out _); // the controller switched it off again
    }

    /// <summary>Everything a controller sees of this node in its ArtPollReply, condensed into one value.</summary>
    private int computeNodeConditions()
    {
        var hash = new HashCode();
        hash.Add(Name);
        hash.Add(ShortName);
        var status = getOwnNodeStatus();
        hash.Add(status.StatusByte1);
        hash.Add(status.StatusByte2);
        hash.Add(status.StatusByte3);
        foreach (var port in portConfigs.OrderBy(p => p.BindIndex).ThenBy(p => p.PortNumber))
        {
            hash.Add(port.BindIndex);
            hash.Add(port.PortNumber);
            hash.Add(port.PortAddress.Combined);
            hash.Add(port.Type);
            hash.Add(port.GoodInput.Byte1);
            hash.Add(port.Input && isTransmitting(port.PortAddress)); // GoodInput "Data received" as reported
            hash.Add(port.GoodOutput.Byte1);
            hash.Add(port.GoodOutput.Byte2);
        }
        return hash.ToHashCode();
    }

    private void checkNodeConditionsChanged()
    {
        if (replyOnChangeSubscribers.IsEmpty)
        {
            lastNodeConditions = null; // start with a fresh baseline when a controller subscribes again
            return;
        }
        var utcNow = DateTime.UtcNow;
        if (utcNow - lastNodeConditionsCheck < NodeConditionsCheckInterval)
            return;
        lastNodeConditionsCheck = utcNow;

        try
        {
            int conditions = computeNodeConditions();
            bool changed = lastNodeConditions.HasValue && lastNodeConditions.Value != conditions;
            lastNodeConditions = conditions;
            if (!changed)
                return;

            foreach (var subscriber in replyOnChangeSubscribers)
            {
                if (utcNow - subscriber.Value.LastPoll > ReplyOnChangeSubscriptionTimeout)
                {
                    replyOnChangeSubscribers.TryRemove(subscriber.Key, out _);
                    continue;
                }
                // Unsolicited reply, unicast, without the random ArtPoll delay
                _ = sendArtPollReply(subscriber.Value.LocalIp, subscriber.Key);
            }
        }
        catch (Exception e) { Logger.LogError(e); }
    }
    #endregion

    #region ArtIpProg
    /// <summary>
    /// Raised when an ArtIpProg (IP reprogramming) is received. The library cannot change the host's network settings;
    /// an application that can, applies the request and sets the resulting values in the event args.
    /// </summary>
    public event EventHandler<ArtIpProgReceivedEventArgs> ArtIpProgReceived;

    private async Task processArtIpProg(ArtIpProg artIpProg, IPv4Address localIp, IPv4Address source)
    {
        if (IsDisposing || IsDisposed || IsDeactivated)
            return;
        try
        {
            var current = ArtNet.GetInterfaceSettings(localIp);
            var args = new ArtIpProgReceivedEventArgs(artIpProg, source, localIp, current.SubnetMask, current.DefaultGateway, current.DhcpEnabled);
            await Events.InvokeAsync(() =>
            {
                ArtIpProgReceived?.InvokeFailSafe(this, args);
                return args;
            });

            // Spec: the node replies with ArtIpProgReply (unicast) - with the settings now in effect.
            using var reply = new ArtIpProgReply(args.Ip, args.SubnetMask, args.DefaultGateway,
                                                 args.DhcpEnabled ? EArtIpProgReplyStatusFlags.EnableDHCP : EArtIpProgReplyStatusFlags.None);
            await TrySendPacket(reply, source);
        }
        catch (Exception ex) { Logger.LogError(ex); }
    }
    #endregion

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
            // Every ArtRdm a gateway sends (its responses) carries its queue state (FifoAvail / FifoMax).
            if (artRDM.RDMMessage.Command.HasFlag(ERDM_Command.RESPONSE))
                getGatewayQueue(source).Update(artRDM);

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
            // A response addressed to another controller is not for us. Our own UID can be in this list too
            // (whenever we saw our own request, e.g. controller and node in one process) - then it IS for us.
            else if (artRDM.Destination != UID && knownControllerRDMUIDs.TryGetValue(artRDM.Destination, out bag))
                return;

            // A request comes from a controller's port that inputs onto Art-Net (InputPortAddress),
            // a response from the node's output port with the RDM devices (OutputPortAddress). This was swapped.
            bool isResponse = artRDM.RDMMessage.Command.HasFlag(ERDM_Command.RESPONSE);
            var ports = RemoteClientsPorts
                .Where(p => IPv4Address.Equals(p.IpAddress, source) && (isResponse ? PortAddress.Equals(p.OutputPortAddress, artRDM.PortAddress) : PortAddress.Equals(p.InputPortAddress, artRDM.PortAddress)))
                .ToList();
            if (ports.Count != 0)
            {
                foreach (var port in ports)
                    port.ProcessArtRDM(artRDM);
            }
        }
        catch (Exception ex) { Logger.LogError(ex); }

        // Duplicate detection: the same transaction within RdmDedupeWindow is processed only once.
        // A timestamp per entry replaces the former Task.Run + Task.Delay(2000) per message;
        // expired entries are removed in the poll cycle (removeExpiredRdmDedupeEntries).
        var ti = new RDM_TransactionID(artRDM.RDMMessage.TransactionCounter, artRDM.Source, artRDM.Destination);
        var utcNow = DateTime.UtcNow;
        while (!artRDMdeBumbReceive.TryAdd(ti, utcNow))
        {
            if (!artRDMdeBumbReceive.TryGetValue(ti, out DateTime seen))
                continue; // removed in between, try to add again
            if (utcNow - seen < RdmDedupeWindow)
                return; // duplicate
            if (artRDMdeBumbReceive.TryUpdate(ti, utcNow, seen))
                break; // expired entry: same transaction number reused later, process it
        }
        try
        {
            if (!artRDM.RDMMessage.Command.HasFlag(ERDM_Command.RESPONSE))
            {
                var eventArgs = new RequestRDMMessageReceivedEventArgs(artRDM.RDMMessage, artRDM.PortAddress);
                // The handler produces the response, so it is awaited - but on the event queue, not on the receive loop.
                await Events.InvokeAsync(() =>
                {
                    RequestRDMMessageReceived?.InvokeFailSafe(this, eventArgs);
                    return eventArgs;
                });
                if (eventArgs.Handled)
                    await sendArtRDM(eventArgs.Response, artRDM.PortAddress, source);
            }
            else
            {
                var eventArgs = new ResponseRDMMessageReceivedEventArgs(artRDM.RDMMessage, artRDM.PortAddress);
                Events.Post(() => ResponseRDMMessageReceived?.InvokeFailSafe(this, eventArgs));
            }
        }
        catch (Exception ex) { Logger.LogError(ex); }
    }

    private static readonly TimeSpan RdmDedupeWindow = TimeSpan.FromSeconds(2);
    private void removeExpiredRdmDedupeEntries()
    {
        var utcNow = DateTime.UtcNow;
        foreach (var entry in artRDMdeBumbReceive)
            if (utcNow - entry.Value >= RdmDedupeWindow)
                artRDMdeBumbReceive.TryRemove(entry.Key, out _);
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
                portConfig.Events = Events; // its RDMUIDReceived is raised on this instance's event queue
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
                if (list.Remove(portConfig) && ReferenceEquals(portConfig.Events, Events))
                    portConfig.Events = null;
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

        try
        {
            // GetOrAdd: the first write for a Port-Address goes through Update as well (so destinationIndex/count
            // are honoured), and two concurrent first writes end up in the same buffer instead of one getting lost.
            DMXSendBag bag = sendDMXBuffer.GetOrAdd(portAddress, pa => new DMXSendBag(pa));
            bag.Update(data, destinationIndex, count);
        }
        catch (Exception e) { Logger.LogError(e); }
    }

    /// <summary>
    /// Received DMX of <paramref name="portAddress"/>, merged with the merge mode configured for that output port
    /// (GoodOutput, changeable by the application or remotely via ArtAddress AcMergeLtp/AcMergeHtp; default HTP).
    /// </summary>
    public byte[] GetReceivedDMX(PortAddress portAddress)
    {
        var port = portConfigs.FirstOrDefault(p => p.Output && p.PortAddress == portAddress);
        return GetReceivedDMX(portAddress, port?.GoodOutput.MergeMode ?? EMergeMode.HTP);
    }

    public byte[] GetReceivedDMX(PortAddress portAddress, EMergeMode mergeMode = EMergeMode.HTP)
    {
        if (IsDisposing || IsDisposed || receivedDMXBuffer.IsEmpty)
            return null;

        // Spec (Data Merging): only sources that did not fail (10 s) are merged; if all failed, the last merge result is held.
        var bags = getMergeSources(portAddress, DateTime.UtcNow);

        if (bags.Count == 0)
            return null;

        // Always a copy: the receive buffers keep changing while the caller uses the result.
        byte[] result = new byte[512];
        if (bags.Count == 1)
        {
            bags[0].CopyTo(result);
            return result;
        }

        switch (mergeMode)
        {
            case EMergeMode.HTP:
                foreach (var bag in bags)
                    bag.MergeHtpInto(result);
                return result;
            case EMergeMode.LTP:
                DMXReceiveBag latest = bags[0];
                foreach (var bag in bags)
                    if (bag.LastUpdate > latest.LastUpdate)
                        latest = bag;
                latest.CopyTo(result);
                return result;
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
                bag = new RDMUID_ReceivedBag(portAddress, bindIndex, rdmuid) { Events = Events };
                if (knownRDMUIDs.TryAdd(rdmuid, bag))
                    Events.Post(() => RDMUIDReceived?.InvokeFailSafe(this, bag));
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

    /// <summary>Time between sending ArtPoll and checking RemoteClients/ports for timeouts. Settable for tests only.</summary>
    internal static TimeSpan TimeoutCheckDelay { get; set; } = TimeSpan.FromSeconds(4);

    private async void TimerSendPoll_Elapsed(object sender, EventArgs e)
    {
        if (IsDeactivated)
            return;

        try // async void: an escaping exception would terminate the host application
        {
            removeExpiredRdmDedupeEntries();
            await triggerSendArtPoll();
            await Task.Delay(TimeoutCheckDelay); // give the polled devices time to reply before checking for timeouts
            if (IsDisposing || IsDisposed)
                return;
            await pollReplyProcessSemaphoreSlim.WaitAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return;
        }
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
                        Events.Post(() => RemoteClientTimedOut?.InvokeFailSafe(this, removed));
                    }
                }
            }

            // Ports of clients that are still alive can time out on their own (e.g. a port was disabled on the node).
            foreach (var rc in remoteClients.Values)
                rc.RemoveTimedOutPorts();
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
            Events.Dispose(); // queued events of a disposed instance are dropped

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

    /// <summary>
    /// RDM transmit queue of one gateway (Art-Net 4, Revision DL: ArtRdm FifoAvail / FifoMax).
    /// <para>
    /// A gateway that implements the queue reports in every ArtRdm it sends how many entries are free (FifoAvail)
    /// and how many there are in total (FifoMax). Before an ArtRdm request is sent to such a gateway, a free entry
    /// is taken; if none is free the request waits (asynchronously, in order) until the gateway reports free entries
    /// again, at most <see cref="MaxWait"/> - then it is sent anyway, so a lost response cannot block RDM to this
    /// gateway for good.
    /// </para>
    /// <para>
    /// FifoMax = 0 means "queue not implemented" (spec). Such gateways are not throttled. As of 2026 no known device
    /// implements the queue: in ~800,000 captured ArtRdm packets from 213 devices (Luminex, ENTTEC, Swisson,
    /// dmXLAN, Martin, ...) both fields were always 0.
    /// </para>
    /// </summary>
    public class GatewayRDMFiFoQueue : INotifyPropertyChanged
    {
        /// <summary>Longest time a request waits for a free entry before it is sent anyway. Settable for tests only.</summary>
        internal static TimeSpan MaxWait { get; set; } = TimeSpan.FromSeconds(2);

        public readonly IPv4Address IpAddress;

        private readonly object sync = new object();
        private readonly LinkedList<TaskCompletionSource<bool>> waiters = new LinkedList<TaskCompletionSource<bool>>();
        private readonly EventDispatcher events;
        private readonly ILogger logger;

        /// <summary>Free entries in the gateway's RDM transmit queue (as last reported, minus requests sent since).</summary>
        public int Available
        {
            get;
            private set
            {
                if (field == value)
                    return;
                field = value;
                onPropertyChanged(nameof(Available));
            }
        } = 0;
        /// <summary>Size of the gateway's RDM transmit queue; 0 = not implemented.</summary>
        public byte Max
        {
            get;
            private set
            {
                if (field == value)
                    return;
                field = value;
                onPropertyChanged(nameof(Max));
                onPropertyChanged(nameof(Implemented));
            }
        } = 0;
        /// <summary>True if the gateway reports an RDM transmit queue (FifoMax &gt; 0); only then requests are throttled.</summary>
        public bool Implemented => Max != 0;

        public event PropertyChangedEventHandler PropertyChanged;
        private void onPropertyChanged(string name)
        {
            var args = new PropertyChangedEventArgs(name);
            EventDispatcher.Raise(events, () => PropertyChanged?.InvokeFailSafe(this, args));
        }

        public GatewayRDMFiFoQueue(IPv4Address ipAddress) : this(ipAddress, null, null)
        {
        }
        internal GatewayRDMFiFoQueue(IPv4Address ipAddress, EventDispatcher events, ILogger logger)
        {
            IpAddress = ipAddress;
            this.events = events;
            this.logger = logger;
        }

        /// <summary>Takes the queue state from an ArtRdm the gateway sent (its FifoAvail / FifoMax).</summary>
        internal void Update(ArtRDM fromGateway)
        {
            if (!fromGateway.RDMMessage.Command.HasFlag(ERDM_Command.RESPONSE))
                return; // only the gateway's own packets (responses) describe its queue

            lock (sync)
            {
                Max = fromGateway.FifoMax;
                Available = Implemented ? fromGateway.FifoAvailable : 0;
                if (!Implemented)
                {
                    // Not (or no longer) implemented: nobody has to wait.
                    foreach (var waiter in waiters)
                        waiter.TrySetResult(true);
                    waiters.Clear();
                    return;
                }
                // Hand the free entries to the waiting requests, oldest first.
                while (Available > 0 && waiters.Count != 0)
                {
                    var waiter = waiters.First.Value;
                    waiters.RemoveFirst();
                    Available--;
                    waiter.TrySetResult(true);
                }
            }
        }

        /// <summary>
        /// Takes a free entry before an ArtRdm request is sent to this gateway. Completes immediately if the queue is
        /// not implemented or an entry is free; otherwise when the gateway reports a free entry, at the latest after
        /// <see cref="MaxWait"/>.
        /// </summary>
        internal async Task AcquireSlotAsync()
        {
            TaskCompletionSource<bool> slot;
            LinkedListNode<TaskCompletionSource<bool>> node;
            lock (sync)
            {
                if (!Implemented)
                    return;
                if (Available > 0 && waiters.Count == 0)
                {
                    Available--;
                    return;
                }
                slot = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                node = waiters.AddLast(slot);
            }

            if (await Task.WhenAny(slot.Task, Task.Delay(MaxWait)).ConfigureAwait(false) == slot.Task)
                return;

            lock (sync)
            {
                if (slot.Task.IsCompleted)
                    return; // got an entry right at the timeout
                waiters.Remove(node);
            }
            logger?.LogWarning($"RDM queue of gateway {IpAddress} reported no free entry for {MaxWait.TotalMilliseconds:0} ms; sending anyway.");
        }
    }
}