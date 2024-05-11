﻿using ArtNetSharp.Messages.Interfaces;
using ArtNetSharp.Misc;
using Microsoft.Extensions.Logging;
using RDMSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace ArtNetSharp.Communication
{
    public abstract class AbstractInstance : IInstance
    {
        protected static ILogger Logger { get; private set; } = null;
        protected bool IsDisposed { get; private set; }
        protected bool IsDisposing { get; private set; }
        bool IDisposableExtended.IsDisposed { get => IsDisposed; }
        bool IDisposableExtended.IsDisposing { get => IsDisposing; }

        public bool IsDeactivated { get { return !ArtNetInstance.Instances.Contains(this); } }

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

        private readonly ConcurrentDictionary<RDMUID, ControllerRDMUID_Bag> knownControllerRDMUIDs = new ConcurrentDictionary<RDMUID, ControllerRDMUID_Bag>();
        public virtual RDMUID UID { get; } = RDMUID.Empty;

        private readonly System.Timers.Timer _timerSendPoll;

        private readonly List<PortConfig> portConfigs = new List<PortConfig>();
        public ReadOnlyCollection<PortConfig> PortConfigs { get => portConfigs.AsReadOnly(); }

        private readonly ConcurrentDictionary<Tuple<PortAddress, IPv4Address>, DMXReceiveBag> receivedDMXBuffer = new ConcurrentDictionary<Tuple<PortAddress, IPv4Address>, DMXReceiveBag>();
        private readonly ConcurrentDictionary<RDM_TransactionID, RDMMessage> artRDMdeBumbReceive = new();

        private readonly struct RDM_TransactionID : IEquatable<RDM_TransactionID>
        {
            private readonly byte Transaction;
            private readonly RDMUID Controller;
            private readonly RDMUID Responder;

            public RDM_TransactionID(byte transaction, RDMUID controller, RDMUID responder)
            {
                Transaction = transaction;
                Controller = controller;
                Responder = responder;
            }
            public override string ToString()
            {
                return $"{Transaction} C: {Controller} R: {Responder}";
            }

            public override bool Equals(object obj)
            {
                return obj is RDM_TransactionID iD && Equals(iD);
            }

            public bool Equals(RDM_TransactionID other)
            {
                return Transaction == other.Transaction &&
                       Controller.Equals(other.Controller) &&
                       Responder.Equals(other.Responder);
            }

            public override int GetHashCode()
            {
                return Transaction.GetHashCode() + Controller.GetHashCode() + Responder.GetHashCode();
            }
        }
        private class DMXReceiveBag: IDisposable
        {
            public byte[] Data { get; private set; } = new byte[512];
            public byte Sequence { get; private set; } = byte.MaxValue;
            public readonly PortAddress PortAddress;
            public readonly IPv4Address Source;
            public DateTime LastUpdate { get; private set; }

            public bool IsDisposed;
            public bool IsDisposing;
            public DMXReceiveBag(ArtDMX artDMX, IPv4Address source)
            {
                PortAddress = new PortAddress(artDMX.Net, artDMX.Address);
                Source = source;
                Update(artDMX,source);
            }
            internal bool Update(ArtDMX artDMX, IPv4Address source)
            {
                if (IsDisposed || IsDisposing)
                    return false;

                if (Source != source)
                    return false;

                bool? check = checkSequence(Sequence, artDMX.Sequence);
                if (check.HasValue && !check.Value)
                    return false;

                Sequence = artDMX.Sequence;

                Array.Copy(artDMX.Data, 0, Data, 0, Math.Min(artDMX.Data.Length, Data.Length));

                LastUpdate = DateTime.UtcNow;
                return true;
            }

            private bool? checkSequence(byte _old, byte _new)
            {
                if (IsDisposed || IsDisposing)
                    return null;

                if (_new == 0)
                    return null; //SpezialCase

                if (_old < _new)
                    return true;

                if (_old > _new && ((byte.MaxValue - _old) < _new))
                    return true;

                return false;
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
        private class DMXSendBag: IDisposable
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

            public DMXSendBag(byte[] data, PortAddress portAddress)
            {
                Update(data);
                PortAddress = portAddress;
            }

            internal async void Update(byte[] data, ushort? startindex = null, ushort? count = null)
            {
                if (IsDisposed || IsDisposing)
                    return;

                await SemaphoreSlim?.WaitAsync();
                try
                {
                    if ((startindex + count) <= Data.Length)
                        Array.Copy(data, startindex.Value, Data, startindex.Value, count.Value);
                    else
                        Array.Copy(data, 0, Data, 0, Math.Min(data.Length, Data.Length));

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
                Sequence++;
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
        public IReadOnlyCollection<RemoteClientPort> RemoteClientsPorts { get { return remoteClients.SelectMany(rc => rc.Value.Ports).ToList().AsReadOnly(); } }

        public event EventHandler<PortAddress> DMXReceived;
        public event EventHandler SyncReceived;
        public event EventHandler<RemoteClient> RemoteClientDiscovered;
        public event EventHandler<RemoteClient> RemoteClientTimedOut;
        public event EventHandler<ArtTimeCode> TimeCodeReceived;
        public event EventHandler<ArtTimeSync> TimeSyncReceived;

        private readonly ConcurrentDictionary<RDMUID, RDMUID_ReceivedBag> knownRDMUIDs = new ConcurrentDictionary<RDMUID, RDMUID_ReceivedBag>();
        public IReadOnlyCollection<RDMUID_ReceivedBag> KnownRDMUIDs;
        public event EventHandler<RDMUID_ReceivedBag> RDMUIDReceived;
        public event EventHandler<ResponderRDMMessageReceivedEventArgs> ResponderRDMMessageReceived;
        public event EventHandler<ControllerRDMMessageReceivedEventArgs> ControllerRDMMessageReceived;

        protected AbstractInstance(ArtNet _artnet)
        {
            _random = new Random();
            ArtNetInstance = _artnet;
            Logger = ApplicationLogging.CreateLogger(this.GetType());

            ArtNetInstance.OnInstanceAdded += ArtNet_OnInstanceAdded;
            ArtNetInstance.OnInstanceRemoved += ArtNet_OnInstanceRemoved;

            _timerSendPoll = new System.Timers.Timer
            {
                Interval = 2500, // Spec 1.4dd page 13
            };
            _timerSendPoll.Elapsed += TimerSendPoll_Elapsed;

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

            _timerSendPoll.Enabled = true;
            await triggerSendArtPoll();
        }

        private void ArtNet_OnInstanceRemoved(object sender, AbstractInstance e)
        {
            if (e != this)
                return;

            _timerSendPoll.Enabled = false;
        }

        void IInstance.PacketReceived(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
                return;

            try
            {
                switch (packet)
                {
                    case ArtPoll artPoll:
                        _ = sendArtPollReply(localIp, sourceIp, artPoll);
                        break;
                    case ArtPollReply artPollReply:
                        processArtPollReply(artPollReply, localIp, sourceIp);
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
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
                return;

            await ArtNetInstance.TrySendPacket(packet, destinationIp);
        }
        protected async Task TrySendBroadcastPacket(AbstractArtPacketCore packet)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
                return;

            await ArtNetInstance.TrySendBroadcastPacket(packet);
        }

        private async Task sendArtPoll(PortAddress targetPortTop = default, PortAddress targetPortBottom = default)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
                return;

            using ArtPoll artPoll = new ArtPoll(OEMProductCode, ESTAManufacturerCode, targetPortTop: targetPortTop, targetPortBottom: targetPortBottom);
            await TrySendBroadcastPacket(artPoll);
        }
        private async Task sendArtPollReply(IPv4Address ownIp, IPv4Address destinationIp, ArtPoll artPoll = null)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated || ArtNetInstance == null)
                return;

            try
            {
                await Task.Delay((int)(800 * _random.NextDouble())); //Art-Net 4 Protocol Release V1.4 Document Revision 1.4dh 19/7/2023 - 23 -

                if (this.IsDisposing || this.IsDisposed || this.IsDeactivated || ArtNetInstance == null)
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

                if (ports.Count != 0)
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
                                                     portConfig.GoodInput,
                                                     portConfig.GoodOutput,
                                                     style: EstCodes);

                            await Task.Delay((int)(50 * _random.NextDouble()));
                            await TrySendPacket(reply, destinationIp);
                        });
                        tasks.Add(task);
                    }
                else if (!artPoll.Flags.HasFlag(EArtPollFlags.EnableTargetedMode))
                {

                    Task task = Task.Run(async () =>
                    {
                        ArtPollReply reply = new ArtPollReply(ownIp,
                                                          ownIp,
                                                          ownMacAddress,
                                                          ShortName,
                                                          Name,
                                                          0,
                                                          nodeStatus,
                                                          MajorVersion,
                                                          MinorVersion,
                                                          net,
                                                          subnet,
                                                          new object[0],
                                                          new object[0],
                                                          OEMProductCode,
                                                          ESTAManufacturerCode,
                                                          nodeReport: nodeReport);
                        await TrySendPacket(reply, destinationIp);
                    });
                    tasks.Add(task);
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
        public async Task SendArtTimeCode(ArtTimeCode timeCode, IPv4Address? ipAddress = null, bool broadcast = true)
        {
            await sendArtTimeCode(timeCode, ipAddress, broadcast);
        }
        private async Task sendArtTimeCode(ArtTimeCode timeCode, IPv4Address? ipAddress = null, bool broadcast = true)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
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
        public async Task SendArtTimeSync(ArtTimeSync timeSync, IPv4Address? ipAddress = null, bool broadcast = true)
        {
            await sendArtTimeSync(timeSync, ipAddress, broadcast);
        }
        private async Task sendArtTimeSync(ArtTimeSync timeSync, IPv4Address? ipAddress = null, bool broadcast = true)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
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
        private async Task sendArtSync()
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
                return;
            using ArtSync artSync = new ArtSync();
            await TrySendBroadcastPacket(artSync);
        }
        #endregion

        #region Send ArtDMX
        internal async Task sendArtDMX(RemoteClientPort remoteClientPort, byte sourcePort, byte[] data, byte sequence, bool broadcast = false)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
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
        public async Task sendArtDMX(IPv4Address ipAddress, PortAddress portAddress, byte sourcePort, byte[] data, byte sequence)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
                return;

            ArtDMX artDMX = new ArtDMX(sequence, sourcePort, portAddress.Net, portAddress.Address, data, sequence);
            await TrySendPacket(artDMX, ipAddress);
        }
        private async Task sendAllArtDMX()
        {
            const double dmxRefreshTime = 1000 / 44.0; // Spec 1.4dh page 56
            const double dmxKeepAliveTime = 800; // Spec 1.4dh page 53
            while (!(this.IsDisposing || this.IsDisposed))
            {
                if (!this.EnableDmxOutput)
                    continue;
                if (this.IsDeactivated)
                    continue;

                try
                {
                    var ports = RemoteClientsPorts.Where(port => port.OutputPortAddress.HasValue && !port.Timouted()).ToList();

                    int sended = 0;
                    foreach (var port in ports)
                        try
                        {
                            if (sendDMXBuffer.TryGetValue(port.OutputPortAddress.Value, out DMXSendBag bag))
                                if ((bag.Updated && (DateTime.UtcNow - bag.LastSended).TotalMilliseconds >= dmxRefreshTime) || (DateTime.UtcNow - bag.LastSended).TotalMilliseconds >= dmxKeepAliveTime)
                                {
                                    bag.LastSended = DateTime.UtcNow;
                                    PortConfig config = portConfigs.FirstOrDefault(pc => PortAddress.Equals(pc.PortAddress, port.OutputPortAddress));
                                    byte sourcePort = config?.PortNumber ?? 0;
                                    await sendArtDMX(port, sourcePort, bag.Data, bag.GetSequence(), config?.ForceBroadcast ?? false);
                                    sended++;
                                    if (config == null)
                                        return;
                                    foreach (IPv4Address ip in config?.AdditionalIPEndpoints)
                                    {
                                        await sendArtDMX(ip, config.PortAddress, sourcePort, bag.Data, bag.GetSequence());
                                        sended++;
                                    }
                                    bag.LastSended = DateTime.UtcNow;
                                }
                        }
                        catch (Exception e) { Logger.LogError(e); }
                    if (EnableSync && sended != 0)
                        await sendArtSync();

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
            List<RDMUID> uids = portConfig.AdditionalRDMUIDs.Select(bag => bag).ToList();
            uids.AddRange(portConfig.DiscoveredRDMUIDs.Select(bag => bag.Uid));
            uids = uids.OrderBy(uid => uid).ToList();
            ushort totalCount = (ushort)uids.Count();
            byte blockCount = 0;
            EArtTodDataCommandResponse command = EArtTodDataCommandResponse.TodFull;
            do
            {
                if (uids.Count == 0)
                    command = EArtTodDataCommandResponse.TodNak;

                var current = uids.Take(200).ToArray();
                artTodData = new ArtTodData(portConfig.PortAddress, portConfig.PortNumber, portConfig.BindIndex, totalCount, blockCount, current, command);
                await TrySendPacket(artTodData, ipAddress);
                blockCount++;
                uids = uids.Except(current).ToList();

                if (uids.Count != 0)
                    command = EArtTodDataCommandResponse.TodNak;
            }
            while (uids.Count != 0);
            uids.Clear();
        }
        public async Task SendArtRDM(RDMMessage rdmMessage)
        {
            if (this.IsDisposed || this.IsDisposing || this.IsDeactivated)
                return;

            if (!rdmMessage.Command.HasFlag(ERDM_Command.RESPONSE) && rdmMessage.SourceUID == RDMUID.Empty)
                rdmMessage.SourceUID = UID;

            if (knownRDMUIDs.TryGetValue(rdmMessage.DestUID, out RDMUID_ReceivedBag uidBag))
                rdmMessage.TransactionCounter = uidBag.NewTransactionNumber();

            if (!rdmMessage.Command.HasFlag(ERDM_Command.RESPONSE)) // Response
            {
                var ports = RemoteClientsPorts.Where(port => port.KnownResponderRDMUIDs.Count != 0).Where(port => port.OutputPortAddress.HasValue && port.KnownResponderRDMUIDs.Any(bag => bag.Uid == rdmMessage.DestUID)).ToList();
                List<Task> tasks = new List<Task>();
                foreach (var port in ports)
                {
                    PortAddress pa = default;
                    if (!rdmMessage.Command.HasFlag(ERDM_Command.RESPONSE) && port.OutputPortAddress.HasValue)
                        pa = port.OutputPortAddress.Value;
                    else if (rdmMessage.Command.HasFlag(ERDM_Command.RESPONSE) && port.InputPortAddress.HasValue)
                        pa = port.InputPortAddress.Value;
                    //Todo Buffer per IP address to prevent Hardware from overflow
                    ArtRDM artRDM = new ArtRDM(pa, rdmMessage);
                    tasks.Add(Task.Run(async () => await TrySendPacket(artRDM, port.IpAddress)));
                }
                await Task.WhenAll(tasks);
            }
        }
        private async Task sendArtRDM(RDMMessage rdmMessage, PortAddress portAddress, IPv4Address ip)
        {
            ArtRDM artRDM = new ArtRDM(portAddress, rdmMessage);
            await TrySendPacket(artRDM, ip);
        }

        #region Send ArtAddress
        public async Task SendArtAddress(ArtAddress artAddress, IPv4Address ipAddress)
        {
            await sendArtAddress(artAddress, ipAddress);
        }
        private async Task sendArtAddress(ArtAddress artAddress, IPv4Address ipAddress)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
                return;

            switch (this.EstCodes)
            {
                case EStCodes.StController:
                case EStCodes.StConfig:
                    await TrySendPacket(artAddress, ipAddress);
                    return;
            }
        }
        #endregion

        #region Send ArtIpProg
        public async Task SendArtIpProg(ArtIpProg artIpProg, IPv4Address ipAddress)
        {
            await sendArtIpProg(artIpProg, ipAddress);
        }
        private async Task sendArtIpProg(ArtIpProg artIpProg, IPv4Address ipAddress)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
                return;

            switch (this.EstCodes)
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
        private void processArtPollReply(ArtPollReply artPollReply, IPv4Address localIp, IPv4Address sourceIp)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
                return;

            if (MajorVersion == artPollReply.MajorVersion
                && MinorVersion == artPollReply.MinorVersion
                && OEMProductCode == artPollReply.OemCode
                && ESTAManufacturerCode == artPollReply.ManufacturerCode
                && IPv4Address.Equals(sourceIp, localIp)
                && IPv4Address.Equals(artPollReply.OwnIp, localIp)
                && string.Equals(artPollReply.ShortName, ShortName)
                && string.Equals(artPollReply.LongName, Name))
                return; //break loopback

            string id = RemoteClient.getIDOf(artPollReply);
            RemoteClient remoteClient = null;

            try
            {
                if (remoteClientsTimeouted.TryRemove(id, out remoteClient))
                {
                    remoteClient.processArtPollReply(artPollReply);
                    _ = add(remoteClient);
                }
                else if (remoteClients.TryGetValue(id, out remoteClient))
                {
                    remoteClient.processArtPollReply(artPollReply);
                }
                else
                {
                    remoteClient = new RemoteClient(artPollReply) { Instance = this };
                    _ = add(remoteClient);
                }
                async Task add(RemoteClient rc)
                {
                    await semaphoreSlimAddRemoteClient.WaitAsync();
                    try
                    {
                        if (remoteClients.TryAdd(rc.ID, rc))
                        {
                            //Delay, to give The Remote CLient time to send all ArtPollReplys
                            await Task.Delay(1000);
                            Logger.LogInformation($"Discovered: {rc.ID}");
                            RemoteClientDiscovered?.InvokeFailSafe(this, rc);
                        }
                    }
                    finally
                    {
                        semaphoreSlimAddRemoteClient.Release();
                    }
                }
            }
            catch (Exception ex) { Logger.LogError(ex); }

            var deadline = 9500; // Spec 1.4dd page 12, doubled to allow one lost reply (6s is allowad, for some delay i add 2500 ms)
            var timoutedClients = remoteClients.Where(p => (DateTime.UtcNow - p.Value.LastSeen).TotalMilliseconds > deadline);
            if (timoutedClients.Count() != 0)
            {
                timoutedClients = timoutedClients.ToList();
                foreach (var rc in timoutedClients)
                {

                    if (remoteClients.TryRemove(rc.Key, out RemoteClient removed))
                        remoteClientsTimeouted.TryAdd(removed.ID, removed);
                    if (removed != null)
                    {
                        Logger.LogInformation($"Timeout: {removed.ID} ({(DateTime.UtcNow - rc.Value.LastSeen).TotalMilliseconds}ms)");
                        RemoteClientTimedOut?.InvokeFailSafe(this, removed);
                    }
                }
            }
            RemoteClients = remoteClients.Select(p => p.Value).ToList().AsReadOnly();
        }
        private void processArtDMX(ArtDMX artDMX, IPv4Address sourceIp)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
                return;

            var port = portConfigs.FirstOrDefault(p => p.Type.HasFlag(EPortType.OutputFromArtNet) && p.Universe == artDMX.Address.Universe && p.Subnet == artDMX.Address.Subnet && p.Net == artDMX.Net);
            if (port == null)
                return;

            bool success = false;
            var key = new Tuple<PortAddress, IPv4Address>(port.PortAddress, sourceIp);
            if (!receivedDMXBuffer.TryGetValue(key, out DMXReceiveBag bag))
            {
                bag = new DMXReceiveBag(artDMX, sourceIp);
                success = receivedDMXBuffer.TryAdd(key, bag);
            }
            else
                success = bag.Update(artDMX, sourceIp);

            if (success)
            {
                DMXReceived?.InvokeFailSafe(this, port.PortAddress);
                port.GoodOutput |= EGoodOutput.DataTransmitted;
            }
        }


        protected async Task processArtTodRequest(ArtTodRequest artTodRequest, IPv4Address source)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
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
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
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
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
                return;

            try
            {
                if (RemoteClientsPorts.Count != 0)
                {
                    var ports = RemoteClientsPorts
                        .Where(p => IPv4Address.Equals(p.IpAddress, source) && PortAddress.Equals(p.OutputPortAddress, artTodData.PortAddress))
                        .ToList();

                    foreach (var port in ports)
                        port.AddResponderRdmUIDs(artTodData.Uids);
                }
            }
            catch (Exception ex) { Logger.LogError(ex); }

            try
            {
                var configs = portConfigs
                    .Where(p => p.Input && p.BindIndex == artTodData.BindIndex && PortAddress.Equals(p.PortAddress, artTodData.PortAddress))
                    .ToList();

                foreach (var config in configs)
                    config.AddDiscoveredRdmUIDs(artTodData.Uids);
            }
            catch (Exception ex) { Logger.LogError(ex); }

            try
            {
                AddRdmUIDs(artTodData.Uids);
            }
            catch (Exception ex) { Logger.LogError(ex); }
        }
        private async void processArtRDM(ArtRDM artRDM, IPv4Address source)
        {
            if (this.IsDisposing || this.IsDisposed || this.IsDeactivated)
                return;
            ControllerRDMUID_Bag bag = null;
            try
            {
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
                    var eventArgs = new ControllerRDMMessageReceivedEventArgs(artRDM.RDMMessage, artRDM.PortAddress);
                    ControllerRDMMessageReceived?.InvokeFailSafe(this, eventArgs);
                    if (eventArgs.Handled)
                        await sendArtRDM(eventArgs.Response, artRDM.PortAddress, source);
                }
                else
                {
                    var eventArgs = new ResponderRDMMessageReceivedEventArgs(artRDM.RDMMessage, artRDM.PortAddress);

                    ResponderRDMMessageReceived?.InvokeFailSafe(this, eventArgs);
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
            if (this.IsDisposing || this.IsDisposed)
                return;

            foreach (var portConfig in portConfigs)
            {
                this.portConfigs.Add(portConfig);
                Logger?.LogDebug($"Added PortConfig {portConfig}");
            }
        }
        public void RemovePortConfig(params PortConfig[] portConfigs)
        {
            if (this.IsDisposing || this.IsDisposed)
                return;

            foreach (var portConfig in portConfigs)
            {
                this.portConfigs.Remove(portConfig);
                Logger?.LogDebug($"Removed instance {portConfig}");
            }
        }

        public void WriteDMXValues(PortAddress portAddress, byte[] data, ushort? startindex = null, ushort? count = null)
        {
            if (this.IsDisposing || this.IsDisposed)
                return;

            if (data.Length > 512)
                throw new ArgumentOutOfRangeException();

            ushort length = (ushort)data.Length;

            if (startindex > length)
                throw new ArgumentOutOfRangeException();


            if ((startindex + count) > length)
                throw new ArgumentOutOfRangeException();

            int _startIndex = 0;
            int _count = length;
            if (startindex.HasValue)
                _startIndex = startindex.Value;
            if (count.HasValue)
                _count = Math.Min(_count, count.Value);


            try
            {
                if (sendDMXBuffer.TryGetValue(portAddress, out DMXSendBag bag))
                    bag.Update(data, startindex, count);
                else
                {
                    var newBag = new DMXSendBag(data, portAddress);
                    sendDMXBuffer.TryAdd(portAddress, newBag);
                }
            }
            catch (Exception e) { Logger.LogError(e); }
        }

        public byte[] GetReceivedDMX(PortAddress portAddress, EMergeMode mergeMode = EMergeMode.HTP)
        {
            if (this.IsDisposing || this.IsDisposed || this.receivedDMXBuffer.IsEmpty)
                return null;

            var bags = receivedDMXBuffer.Values.Where(v => v.PortAddress == portAddress).ToList();

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
        public RDMUID[] GetReceivedRDMUIDs()
        {
            if (this.IsDisposing || this.IsDisposed)
                return null;

            return KnownRDMUIDs.Where(k => !k.Timouted()).Select(k => k.Uid).ToArray();
        }

        protected async Task PerformRDMDiscovery(PortAddress? portAddress = null, bool flush = false, bool broadcast = false)
        {
            if (this.IsDisposing || this.IsDisposed)
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

        protected virtual async Task PerformRDMDiscoverOnOutput(PortConfig portConfig)
        {
            await Task.Delay(500);
        }

        protected virtual NodeStatus GetOwnNodeStatus()
        {
            return NodeStatus.None;
        }
        private NodeStatus getOwnNodeStatus()
        {
            NodeStatus nodeStatus = GetOwnNodeStatus() | NodeStatus.NodeSupports15BitPortAddress;

            if (SupportRDM)
                nodeStatus |= NodeStatus.RDMSupported;

            return nodeStatus;
        }
        private void AddRdmUIDs(params RDMUID[] rdmuids)
        {
            if (rdmuids.Length == 0)
                return;

            foreach (RDMUID rdmuid in rdmuids)
            {
                if (knownRDMUIDs.TryGetValue(rdmuid, out RDMUID_ReceivedBag bag))
                    bag.Seen();
                else
                {
                    bag = new RDMUID_ReceivedBag(rdmuid);
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
            if (this.IsDisposed || this.IsDisposing || this.IsDeactivated)
                return;
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

        private async void TimerSendPoll_Elapsed(object sender, ElapsedEventArgs e)
        {
            await triggerSendArtPoll();
        }

        void IDisposable.Dispose()
        {
            if (this.IsDisposed || this.IsDisposing)
                return;

            Logger.LogInformation($"Disposing {Name}");
            this.IsDisposing = true;
            try
            {

                if (!this.IsDeactivated)
                    ArtNetInstance.RemoveInstance(this);

                ArtNetInstance.OnInstanceAdded -= ArtNet_OnInstanceAdded;
                ArtNetInstance.OnInstanceRemoved -= ArtNet_OnInstanceRemoved;

                _timerSendPoll.Elapsed -= TimerSendPoll_Elapsed;
                _timerSendPoll.Enabled = false;

                foreach (var rBuffer in receivedDMXBuffer.Values)
                    rBuffer.Dispose();
                receivedDMXBuffer.Clear();

                foreach (var sBuffer in sendDMXBuffer.Values)
                    sBuffer.Dispose();
                sendDMXBuffer.Clear();

                RemovePortConfig(portConfigs.ToArray());

                portConfigs.Clear();
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
                this.IsDisposed = true;
                this.IsDisposing = false;
                Logger.LogInformation($"Disposed {Name}");
                GC.SuppressFinalize(this);
            }
        }

        protected virtual void Dispose()
        {

        }
    }
}