using ArtNetSharp.Communication;
using Microsoft.Extensions.Logging;
using RDMSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("ArtNetTests")]
namespace ArtNetSharp;

public class ArtNet : IDisposable
{
    private static readonly Random _random = new Random();
    private static readonly ILogger<ArtNet> Logger = Logging.CreateLogger<ArtNet>();

    public static ArtNet Instance
    {
        get
        {
            field ??= new ArtNet();
            return field;
        }
    }

    public event EventHandler<AbstractInstance> OnInstanceAdded;
    public event EventHandler<AbstractInstance> OnInstanceRemoved;

    private readonly ConcurrentDictionary<IPv4Address, MACAddress> ipTomacAddressCache = new ConcurrentDictionary<IPv4Address, MACAddress>();

    /// <summary>
    /// Unicast destination -> the one NetworkClientBag whose interface the OS routes it through.
    /// Avoids fanning every unicast packet out to all NICs. Cleared whenever the NIC set changes.
    /// A null value caches "no matching NIC".
    /// </summary>
    private readonly ConcurrentDictionary<IPv4Address, NetworkClientBag> unicastRoutes = new ConcurrentDictionary<IPv4Address, NetworkClientBag>();

    private readonly ConcurrentDictionary<uint, AbstractInstance> instances = new ConcurrentDictionary<uint, AbstractInstance>();

    // Unique dictionary keys for instances and network clients (random keys could collide,
    // and a failed TryAdd silently dropped the instance/client).
    private static int keyCounter;
    private static uint nextKey() => unchecked((uint)Interlocked.Increment(ref keyCounter));
    public ReadOnlyCollection<AbstractInstance> Instances => instances.Values.ToList().AsReadOnly();

    private readonly ConcurrentDictionary<uint, NetworkClientBag> networkClients = new ConcurrentDictionary<uint, NetworkClientBag>();
    public IReadOnlyCollection<NetworkClientBag> NetworkClients => networkClients.Values.ToList().AsReadOnly();

    private System.Timers.Timer _updateNetworkClientsTimer = null;

    public bool IsDisposing { get; private set; }
    public bool IsDisposed { get; private set; }

    internal NetworkLoopAdapter LoopNetwork
    {
        get;
        set
        {
            if (field != null)
                field.DataReceived -= LoopNetwork_DataReceived;
            field = value;
            if (field != null)
                field.DataReceived += LoopNetwork_DataReceived;
        }
    }

    private async void LoopNetwork_DataReceived(object sender, EventArgs e)
    {
        try // async void: an escaping exception would terminate the host application
        {
            NetworkLoopAdapter adapter = (NetworkLoopAdapter)sender;
            var bag = await adapter.Receive();
            if (!Tools.TryDeserializePacket(bag.Data, out var packet))
                return;
            if (Logger.IsEnabled(LogLevel.Trace))
                Logger.LogTrace($"Received Loop Package {packet}");
            // The loop adapter delivers synchronously from inside Send(), so dispatch asynchronously
            // here to avoid re-entrant send->receive->send chains.
            processPacket(packet, new IPv4Address("3.3.3.3"), new IPv4Address("3.3.3.3"), dispatchAsync: true);
        }
        catch (Exception ex) { Logger.LogError(ex); }
    }

    public class NetworkClientBag : IDisposable
    {
        private static readonly ILogger<NetworkClientBag> Logger = Logging.CreateLogger<NetworkClientBag>();
        private readonly IPEndPoint broadcastEndpoint;
        public readonly IPAddress BroadcastIpAddress;
        public readonly UnicastIPAddressInformation UnicastIPAddressInfo;
        public IPAddress LocalIpAddress => UnicastIPAddressInfo.Address;
        public IPAddress IPv4Mask => UnicastIPAddressInfo.IPv4Mask;

        private UdpClient _client = null;
        /// <summary>
        /// Receive-only socket bound to the directed broadcast address of this interface.
        /// Needed on Linux and macOS: a socket bound to the interface's unicast IP does not
        /// receive broadcasts there (Windows delivers them anyway). See GitHub issue #11.
        /// </summary>
        private UdpClient _broadcastClient = null;
        private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public bool ClientAlive { get; private set; } = false;

        public bool Enabled
        {
            get; set
            {
                if (field == value)
                    return;
                field = value;

                Logger.LogDebug($"Client ({LocalIpAddress ?? BroadcastIpAddress}) {(field ? "Enabled" : "Disabled")}");
            }
        } = true;
        public bool IsDisposing { get; private set; }
        public bool IsDisposed { get; private set; }

        public event EventHandler<Tuple<IPv4Address, UdpReceiveResult>> ReceivedData;

        private NetworkClientBag(IPAddress broadcastIpAddress, UnicastIPAddressInformation unicastIPAddressInformation)
        {
            UnicastIPAddressInfo = unicastIPAddressInformation;
            if (unicastIPAddressInformation.Address.Equals(IPAddress.Loopback))
                broadcastIpAddress = IPAddress.Loopback;

            BroadcastIpAddress = broadcastIpAddress;
            broadcastEndpoint = new IPEndPoint(broadcastIpAddress, Constants.ARTNET_PORT);
            Logger?.LogTrace($"Create Client ({LocalIpAddress})");
        }
        internal static async Task<NetworkClientBag> CreateInstance(IPAddress broadcastIpAddress, UnicastIPAddressInformation unicastIPAddressInformation)
        {
            NetworkClientBag ncb = new NetworkClientBag(broadcastIpAddress, unicastIPAddressInformation);
            await ncb.openClient();
            return ncb;
        }

        private async Task closeClient()
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                ClientAlive = false;
                _client?.Close();
                (_client as IDisposable)?.Dispose();
                _broadcastClient?.Close();
                (_broadcastClient as IDisposable)?.Dispose();
                _broadcastClient = null;
                matchIpCache.Clear();
                Logger?.LogTrace($"Client ({LocalIpAddress}) Cleared");
            }
            catch (Exception e) { Logger.LogDebug(e); }
            finally { semaphoreSlim?.Release(); }
        }
        private async Task openClient()
        {
            if (_client?.Client != null || IsDisposed || IsDisposing)
                return;

            await closeClient();

            while (!IsNetworkAvailable())
                await Task.Delay(100 + (int)(100 * _random.NextDouble()));

            await semaphoreSlim.WaitAsync();
            try
            {
                Logger?.LogTrace($"Client ({LocalIpAddress}): Test Socket");
                using (Socket testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    testSocket.EnableBroadcast = true;
                    await testSocket.ConnectAsync(BroadcastIpAddress, Constants.ARTNET_PORT);
                }
                Logger?.LogTrace($"Client ({LocalIpAddress}): Socket is active");

                Logger?.LogTrace($"Client ({LocalIpAddress}): initialize");
                _client = new UdpClient();
                _client.ExclusiveAddressUse = false;
                _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _client.EnableBroadcast = true;

                IPAddress endpointIp = getEndointIP();
                IPEndPoint localEp = new IPEndPoint(endpointIp, Constants.ARTNET_PORT);
                _client.Client.Bind(localEp);
                ClientAlive = true;
                _ = StartListening(_client, reopenOnError: true);
                openBroadcastClient();
                Logger?.LogTrace($"Client ({LocalIpAddress}): initialized!");
            }
            catch (Exception e) { Logger?.LogError(e, $"Client ({LocalIpAddress}): Error on initialize"); }
            finally { semaphoreSlim?.Release(); }
        }

        internal bool NeedsBroadcastClient =>
            !Tools.IsWindows()
            && LocalIpAddress.AddressFamily == AddressFamily.InterNetwork
            && !IPAddress.IsLoopback(LocalIpAddress)
            && !BroadcastIpAddress.Equals(LocalIpAddress) // e.g. /32 interfaces, there is no separate broadcast address
            && !BroadcastIpAddress.Equals(IPAddress.Broadcast);

        internal bool BroadcastClientAlive => _broadcastClient?.Client != null;

        private void openBroadcastClient()
        {
            if (!NeedsBroadcastClient)
                return;

            try
            {
                _broadcastClient = new UdpClient();
                _broadcastClient.ExclusiveAddressUse = false;
                _broadcastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _broadcastClient.EnableBroadcast = true;
                _broadcastClient.Client.Bind(new IPEndPoint(BroadcastIpAddress, Constants.ARTNET_PORT));
                _ = StartListening(_broadcastClient, reopenOnError: false);
                Logger?.LogTrace($"Client ({LocalIpAddress}): broadcast receiver bound to {BroadcastIpAddress}");
            }
            catch (Exception e)
            {
                // Unicast still works without it, so this is not fatal for the client.
                Logger?.LogWarning(e, $"Client ({LocalIpAddress}): Could not bind broadcast receiver to {BroadcastIpAddress}, broadcasts will not be received on this interface");
                (_broadcastClient as IDisposable)?.Dispose();
                _broadcastClient = null;
            }
        }
        private IPAddress getEndointIP()
        {
            IPAddress endpointIp = IPAddress.Any;
            if (Tools.IsWindows())
                endpointIp = LocalIpAddress;
            if (Tools.IsMac())
                endpointIp = LocalIpAddress;
            if (Tools.IsLinux())
                endpointIp = LocalIpAddress;

            return endpointIp;
        }

        private async Task StartListening(UdpClient client, bool reopenOnError)
        {
            try
            {
                while (true)
                {
                    if (IsDisposed || IsDisposing)
                        return;

                    UdpReceiveResult received = await client.ReceiveAsync();

                    if (!Tools.IsWindows())
                        if (!Tools.IsInSubnet(LocalIpAddress, UnicastIPAddressInfo.IPv4Mask, received.RemoteEndPoint.Address))
                        {
                            if (Logger?.IsEnabled(LogLevel.Trace) == true)
                                Logger.LogTrace($"Drop Packet Local:{LocalIpAddress}, Mask: {UnicastIPAddressInfo.IPv4Mask}, Remote: {received.RemoteEndPoint.Address}");
                            continue;
                        }

                    if (Enabled)
                        ReceivedData?.InvokeFailSafe(this, new Tuple<IPv4Address, UdpReceiveResult>(LocalIpAddress, received));
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) when (!reopenOnError || IsDisposed || IsDisposing) { }
            catch (Exception e)
            {
                Logger.LogError(e);
                if (reopenOnError)
                    _ = openClient();
            }
        }

        // Thread-safe: MatchIP is called concurrently from many send tasks.
        private readonly ConcurrentDictionary<IPAddress, bool> matchIpCache = new ConcurrentDictionary<IPAddress, bool>();
        public async Task<bool> MatchIP(IPAddress ip)
        {
            if (ip == null)
                return false;
            if (IPAddress.IsLoopback(ip))
                return IPAddress.IsLoopback(LocalIpAddress);
            if (ip.Equals(IPAddress.Any))
                return false;

            if (matchIpCache.TryGetValue(ip, out bool cached))
                return cached;

            var _ip = await GetLocalIP(ip);
            bool match = IPAddress.Equals(LocalIpAddress, _ip);
            matchIpCache[ip] = match;
            return match;
        }
        public Task<IPAddress> GetLocalIP(IPAddress ip) => GetRoutedLocalIP(ip);

        /// <summary>
        /// Asks the OS which local address it would use to reach <paramref name="ip"/>
        /// (connecting a UDP socket sends nothing, it only resolves the route).
        /// </summary>
        internal static async Task<IPAddress> GetRoutedLocalIP(IPAddress ip)
        {
            try
            {
                using Socket testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                testSocket.EnableBroadcast = true;
                await testSocket.ConnectAsync(ip, Constants.ARTNET_PORT);
                return ((IPEndPoint)testSocket.LocalEndPoint).Address;
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }

            return null;
        }

        internal async Task TrySendPacket(AbstractArtPacketCore packet, IPv4Address destinationIp)
        {
            if (!Enabled || IsDisposed || IsDisposing)
                return;

            if (!await MatchIP(destinationIp))
                return;

            byte[] data = packet.GetPacket();
            await semaphoreSlim.WaitAsync();
            try
            {
                if (!ClientAlive || _client?.Client == null)
                    return;

                await _client.SendAsync(data, data.Length, new IPEndPoint(destinationIp, Constants.ARTNET_PORT));
                //#if DEBUG
                //  Logger.LogTrace($"Send Packet to {destinationIp} -> {packet}");
                //#endif
                return;
            }
            catch (SocketException se)
            {
                Logger?.LogWarning(se, $"On Client {LocalIpAddress}, could not send packet: {packet}, Client will be Disposed");
                Dispose();
            }
            catch (Exception e)
            {
                Logger?.LogWarning(e, $"On Client {LocalIpAddress}, could not send packet: {packet}");
            }
            finally
            {
                semaphoreSlim?.Release();
                await openClient();
            }
        }
        internal async Task TrySendBroadcastPacket(AbstractArtPacketCore packet)
        {
            if (!Enabled || IsDisposed || IsDisposing)
                return;

            byte[] data = packet.GetPacket();
            await semaphoreSlim.WaitAsync();
            try
            {
                if (!ClientAlive || _client?.Client == null)
                    return;
                await _client.SendAsync(data, data.Length, broadcastEndpoint);
                //#if DEBUG
                //                    Logger.LogTrace($"Send Packet to {broadcastEndpoint.Address} -> {packet}");
                //#endif
                return;
            }
            catch (SocketException se)
            {
                Logger?.LogWarning(se, $"On Client {LocalIpAddress}, could not send packet: {packet}, Client will be Disposed");
                Dispose();
            }
            catch (Exception e)
            {
                Logger?.LogWarning(e, $"On Client {LocalIpAddress}, could not send packet: {packet}");
            }
            finally
            {
                semaphoreSlim?.Release();
                await openClient();
            }
        }

        public void Dispose()
        {
            if (IsDisposed || IsDisposing)
                return;
            IsDisposing = true;
            _ = closeClient();
            semaphoreSlim.Dispose();
            semaphoreSlim = null;
            IsDisposed = true;
            IsDisposing = false;
        }
    }

    internal ArtNet([CallerFilePath] string caller = "", [CallerLineNumber] int line = -1)
    {
        Logger?.LogTrace($"Initialize {caller} (Line: {line})");
        _updateNetworkClientsTimer = new System.Timers.Timer();
        _updateNetworkClientsTimer.Interval = 1000;
        _updateNetworkClientsTimer.Enabled = true;
        _updateNetworkClientsTimer.Elapsed += UpdateNetworkClientsTimer_Elapsed;
        try
        {
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
        }
        catch (Exception e) { Logger?.LogDebug(e); } // not supported on every platform, the periodic scan still covers it
        updateNetworkClients();
        Logger?.LogTrace($"Initialized!");
    }
    ~ArtNet()
    {
        ((IDisposable)this).Dispose();
    }

    internal class NetworkLoopAdapter
    {
        internal readonly struct LoopDataBag
        {
            public readonly byte[] Data;

            public LoopDataBag(byte[] data) : this()
            {
                Data = data;
            }
        }
        private readonly ConcurrentQueue<LoopDataBag> bag;
        public event EventHandler DataReceived;

        public readonly IPv4Address Mask;
        public NetworkLoopAdapter(IPv4Address mask)
        {
            bag = new ConcurrentQueue<LoopDataBag>();
            Mask = mask;
        }
        public void Send(byte[] _data)
        {
            bag.Enqueue(new LoopDataBag(_data));
            DataReceived?.InvokeFailSafe(this, EventArgs.Empty);
        }

        public async Task<LoopDataBag> Receive()
        {
            LoopDataBag b;
            while (!bag.TryDequeue(out b))
                await Task.Delay(1);

            return b;
        }
    }

    private static readonly List<ILoggerProvider> loggerProviders = new List<ILoggerProvider>();
    public static void AddLoggProvider(ILoggerProvider loggerProvider)
    {
        if (loggerProviders.Contains(loggerProvider))
            return;
        Logging.LoggerFactory.AddProvider(loggerProvider);
        loggerProviders.Add(loggerProvider);
    }

    private void UpdateNetworkClientsTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) => updateNetworkClients();

    private void ReceivedData(object sender, Tuple<IPv4Address, UdpReceiveResult> e)
    {
        if (IsDisposed || IsDisposing)
            return;
        IPAddress localIpAddress = e.Item1;
        UdpReceiveResult result = e.Item2;
        IPEndPoint RemoteIpEndPoint = result.RemoteEndPoint;
        IPv4Address sourceIp = RemoteIpEndPoint.Address;
        byte[] received = result.Buffer;
        try
        {
            if (Tools.TryDeserializePacket(received, out var packet))
            {
                //var nic = networkClients.Values.FirstOrDefault(n => Tools.IsInSubnet(n.LocalIpAddress, n.IPv4Mask, sourceIp));
                //if (nic != null)
                //{
                //    //Logger?.LogTrace($"Process Network Packet:{packet} {Environment.NewLine} Local:{nic.LocalIpAddress}, Mask: {nic.IPv4Mask}, Remote: {sourceIp}");
                //    processPacket(packet, nic.LocalIpAddress, sourceIp);
                //}
                processPacket(packet, localIpAddress, sourceIp);
                return;
            }

            Logger.LogWarning($"Can't deserialize Data to ArtNet-Packet from {sourceIp}");
        }
        catch (ObjectDisposedException ed) { Logger.LogTrace(ed); }
        catch (SocketException se) { Logger.LogTrace(se); }
        catch (Exception ex) { Logger.LogError(ex); }
    }
    private void processPacket(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp, bool dispatchAsync = false)
    {
        // Guarded: the interpolated string (incl. packet.ToString()) would otherwise be built for every packet.
        if (Logger.IsEnabled(LogLevel.Trace))
            Logger.LogTrace($"Received Packet from {sourceIp} -> {packet}");

        // Dispatch synchronously on the receive loop: keeps ArtDmx in arrival order (so the
        // Sequence check does not discard re-ordered frames) and avoids a thread-pool hop per packet.
        // The instance handlers start their slow work (replies, RDM, ...) as separate tasks themselves.
        foreach (var inst in instances)
        {
            var instance = (IInstance)inst.Value;
            if (dispatchAsync)
                Task.Run(() => dispatch(instance));
            else
                dispatch(instance);
        }

        void dispatch(IInstance instance)
        {
            try
            {
                instance.PacketReceived(packet, localIp, sourceIp);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }

    public static bool IsNetworkAvailable(long? minimumSpeed = null)
    {
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // discard because of standard reasons
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                if (minimumSpeed != null)
                {
                    // this allow to filter modems, serial, etc.
                    // I use 10000000 as a minimum speed for most cases
                    if (ni.Speed < minimumSpeed)
                        continue;
                }

                // discard virtual cards (virtual box, virtual pc, etc.)
                if (ni.Description.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ni.Name.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                // discard "Microsoft Loopback Adapter", it will not show as NetworkInterfaceType.Loopback but as Ethernet Card.
                if (ni.Description.Equals("Microsoft Loopback Adapter", StringComparison.OrdinalIgnoreCase))
                    continue;

                return true;
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }

        return false;
    }
    public MACAddress GetMacAdress(IPv4Address ip)
    {
        try
        {
            if (ipTomacAddressCache.TryGetValue(ip, out MACAddress cached))
                return cached;
            MACAddress mac;
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

            IPAddress _ipToTest = ip;

            var nicWithThisIP = nics.FirstOrDefault(nic => nic.GetIPProperties().UnicastAddresses.Any(_ip => IPAddress.Equals(_ip.Address, _ipToTest)));
            if (nicWithThisIP != null && nicWithThisIP.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                mac = new MACAddress(nicWithThisIP.GetPhysicalAddress().GetAddressBytes());
            else
                mac = new MACAddress();

            ipTomacAddressCache[ip] = mac;
            return mac;
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }

        return new MACAddress();
    }
    private readonly SemaphoreSlim updateNetworkSenaphoreSlim = new SemaphoreSlim(1);

    /// <summary>
    /// Enumerating all interfaces is expensive, so it is done on OS address-change notifications
    /// and otherwise only every <see cref="FullNicScanInterval"/> as a fallback. The 1s timer only
    /// does the cheap removal of dead clients.
    /// </summary>
    private static readonly TimeSpan FullNicScanInterval = TimeSpan.FromSeconds(10);
    private DateTime lastFullNicScan = DateTime.MinValue;
    private volatile bool nicScanRequested = true;

    private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e) => requestNicScan();
    private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e) => requestNicScan();
    private void requestNicScan()
    {
        nicScanRequested = true;
        updateNetworkClients();
    }

    private async void updateNetworkClients()
    {
        if (LoopNetwork != null)
            return;

        if (IsDisposed || IsDisposing || updateNetworkSenaphoreSlim == null)
            return;

        // Remove clients whose socket died (e.g. after a send error), so they get recreated below.
        foreach (var dead in networkClients.Where(nc => !nc.Value.ClientAlive).ToList())
            if (networkClients.TryRemove(dead.Key, out NetworkClientBag ncb))
            {
                ncb.ReceivedData -= ReceivedData;
                ncb.Dispose();
                unicastRoutes.Clear();
                nicScanRequested = true;
                Logger?.LogDebug($"Removed dead NetworkClient {ncb.LocalIpAddress}");
            }

        if (!nicScanRequested && DateTime.UtcNow - lastFullNicScan < FullNicScanInterval)
            return;

        if (updateNetworkSenaphoreSlim.CurrentCount == 0)
            return; // a scan is running; nicScanRequested stays set, so the next tick picks it up

        await updateNetworkSenaphoreSlim?.WaitAsync();
        try
        {
            nicScanRequested = false;
            lastFullNicScan = DateTime.UtcNow;
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface @interface in interfaces)
            {
                //if (@interface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (@interface.OperationalStatus != OperationalStatus.Up)
                    continue;
                UnicastIPAddressInformationCollection unicastIpInfoCol = @interface.GetIPProperties().UnicastAddresses;
                foreach (UnicastIPAddressInformation ipInfo in unicastIpInfoCol)
                {
                    if (networkClients.Values.ToList().Any(nc => nc.LocalIpAddress.Equals(ipInfo.Address)))
                        continue;

                    uint ipAddress = BitConverter.ToUInt32(ipInfo.Address.GetAddressBytes(), 0);
                    uint ipMaskV4 = BitConverter.ToUInt32(ipInfo.IPv4Mask.GetAddressBytes(), 0);
                    uint broadCastIpAddress = ipAddress | ~ipMaskV4;

                    byte[] bytes = BitConverter.GetBytes(broadCastIpAddress);
                    if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255) // Limited Broadcast: Art-Net packets should not be broadcast to the Limited Broadcast address of 255.255.255.255.
                        continue;// 1.4dh 19/7/2023 - 10 -

                    var ncb = await NetworkClientBag.CreateInstance(new IPAddress(bytes), ipInfo);
                    if (ncb.ClientAlive)
                    {
                        ncb.ReceivedData += ReceivedData;
                        networkClients.TryAdd(nextKey(), ncb);
                        unicastRoutes.Clear();
                        Logger?.LogDebug($"Added NetworkClient {ncb.LocalIpAddress}");
                    }
                    else
                    {
                        Logger?.LogWarning($"NetworkClient {ncb.LocalIpAddress} client not alive, disposing and not adding.");
                        ncb.Dispose();
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger?.LogError(e);
        }
        finally
        {
            if (!(IsDisposed || IsDisposing))
                updateNetworkSenaphoreSlim?.Release();
        }
    }

    public void AddInstance(params AbstractInstance[] instances)
    {
        foreach (var instance in instances)
        {
            if (instance == null || this.instances.Any(i => i.Value == instance))
                continue; // already added: skip only this one, not the remaining instances
            if (this.instances.TryAdd(nextKey(), instance))
            {
                // Before OnInstanceAdded: its handlers already check IsDeactivated.
                // Only for the ArtNet the instance was created for (same semantics as the former Instances.Contains check).
                if (ReferenceEquals(instance.ArtNetInstance, this))
                    instance.RegisteredIn = this;
                Logger?.LogDebug($"Added instance {instance.GetType().Name}: {instance.Name} ({instance.ShortName})");
                OnInstanceAdded?.InvokeFailSafe(this, instance);
            }
        }
    }
    public void RemoveInstance(params AbstractInstance[] instances)
    {
        foreach (var instance in instances)
        {
            if (instance == null)
                continue;
            var toRemove = this.instances.FirstOrDefault(i => i.Value == instance);
            if (toRemove.Value == null)
                continue; // not registered: skip only this one, not the remaining instances
            if (this.instances.TryRemove(toRemove.Key, out _))
            {
                if (ReferenceEquals(instance.RegisteredIn, this))
                    instance.RegisteredIn = null;
                Logger?.LogDebug($"Removed instance {instance.Name} ({instance.ShortName})");
                OnInstanceRemoved?.InvokeFailSafe(this, instance);
            }
        }
    }

    internal async Task TrySendPacket(AbstractArtPacketCore packet, IPv4Address destinationIp)
    {
        if (IsDisposed || IsDisposing)
            return;
        if (LoopNetwork == null)
        {
            // A unicast destination is reached through exactly one interface, so send through
            // that one only instead of starting a task per NIC for every packet.
            NetworkClientBag ncb = await resolveUnicastRoute(destinationIp);
            if (ncb != null)
                await ncb.TrySendPacket(packet, destinationIp);
        }
        else
        {
            LoopNetwork.Send(packet);
        }
    }

    private async Task<NetworkClientBag> resolveUnicastRoute(IPv4Address destinationIp)
    {
        if (unicastRoutes.TryGetValue(destinationIp, out NetworkClientBag cached) && !(cached?.IsDisposed ?? false))
            return cached;

        IPAddress destination = destinationIp;
        NetworkClientBag found = null;
        if (!destination.Equals(IPAddress.Any))
        {
            IPAddress local = IPAddress.IsLoopback(destination) ? IPAddress.Loopback : await NetworkClientBag.GetRoutedLocalIP(destination);
            if (local != null)
                found = networkClients.Values.FirstOrDefault(nc => !nc.IsDisposed && nc.LocalIpAddress.Equals(local));
        }

        unicastRoutes[destinationIp] = found;
        return found;
    }
    internal async Task TrySendBroadcastPacket(AbstractArtPacketCore packet)
    {
        if (IsDisposed || IsDisposing)
            return;

        if (LoopNetwork == null)
        {
            List<Task> tasks = new List<Task>();
            foreach (var ncb in networkClients.Values)
                tasks.Add(Task.Run(async () => await ncb.TrySendBroadcastPacket(packet)));
            await Task.WhenAll(tasks);
        }
        else
        {
            LoopNetwork.Send(packet);
        }
    }

    void IDisposable.Dispose()
    {
        if (IsDisposed || IsDisposing)
            return;
        IsDisposing = true;

        Logger?.LogCritical($"Dispose ArtNet");

        if (_updateNetworkClientsTimer != null)
        {
            _updateNetworkClientsTimer.Enabled = false;
            _updateNetworkClientsTimer.Elapsed -= UpdateNetworkClientsTimer_Elapsed;
            _updateNetworkClientsTimer = null;
        }
        try
        {
            NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged -= NetworkChange_NetworkAvailabilityChanged;
        }
        catch (Exception e) { Logger?.LogDebug(e); }

        foreach (var instance in instances)
            ((IDisposable)instance.Value).Dispose();
        instances.Clear();
        foreach (var net in networkClients)
        {
            net.Value.Enabled = false;
            net.Value.Dispose();
        }

        networkClients?.Clear();
        unicastRoutes.Clear();
        loggerProviders?.Clear();

        updateNetworkSenaphoreSlim.Release();
        updateNetworkSenaphoreSlim.Dispose();

        ipTomacAddressCache.Clear();
        OnInstanceAdded = null;
        OnInstanceRemoved = null;
        LoopNetwork = null;
        IsDisposed = true;
        IsDisposing = false;

        GC.SuppressFinalize(this);
    }
}