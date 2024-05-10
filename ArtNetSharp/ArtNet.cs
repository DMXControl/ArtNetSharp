using ArtNetSharp.Communication;
using Microsoft.Extensions.Logging;
using RDMSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("ArtNetTests")]
namespace ArtNetSharp
{
    public class ArtNet : IDisposable
    {
        private static readonly Random _random = new Random();
        private static ILogger<ArtNet> Logger = null;
        private static ArtNet instance;
        public static ArtNet Instance
        {
            get
            {
                instance ??= new ArtNet();
                return instance;
            }
        }

        public event EventHandler<AbstractInstance> OnInstanceAdded;
        public event EventHandler<AbstractInstance> OnInstanceRemoved;

        private readonly Dictionary<IPv4Address, MACAddress> ipTomacAddressCache = new Dictionary<IPv4Address, MACAddress>();

        private readonly ConcurrentDictionary<uint, AbstractInstance> instances = new ConcurrentDictionary<uint, AbstractInstance>();
        public ReadOnlyCollection<AbstractInstance> Instances { get => instances.Values.ToList().AsReadOnly(); }

        private readonly ConcurrentDictionary<uint,NetworkClientBag> networkClients = new ConcurrentDictionary<uint, NetworkClientBag>();
        public IReadOnlyCollection<NetworkClientBag> NetworkClients => networkClients.Values.ToList().AsReadOnly();

        private System.Timers.Timer _updateNetworkClientsTimer = null;

        public bool IsDisposing { get; private set; }
        public bool IsDisposed { get; private set; }

        private NetworkLoopAdapter loopNetwork;
        internal NetworkLoopAdapter LoopNetwork
        {
            get
            {
                return loopNetwork;
            }
            set
            {
                if (loopNetwork != null)
                    loopNetwork.DataReceived -= LoopNetwork_DataReceived;
                loopNetwork = value;
                if (loopNetwork != null)
                    loopNetwork.DataReceived += LoopNetwork_DataReceived;
            }
        }

        private async void LoopNetwork_DataReceived(object sender, EventArgs e)
        {
            NetworkLoopAdapter adapter = (NetworkLoopAdapter)sender;
            var bag = await adapter.Receive();
            Tools.TryDeserializePacket(bag.Data, out var packet);
            Logger.LogTrace($"Received Loop Package {packet}");
            processPacket(packet, new IPv4Address("3.3.3.3"), new IPv4Address("3.3.3.3"));
        }

        public class NetworkClientBag: IDisposable
        {
            private static readonly ILogger<NetworkClientBag> Logger = ApplicationLogging.CreateLogger<NetworkClientBag>();
            private readonly IPEndPoint broadcastEndpoint;
            public readonly IPAddress BroadcastIpAddress;
            public readonly UnicastIPAddressInformation UnicastIPAddressInfo;
            public IPAddress LocalIpAddress => UnicastIPAddressInfo.Address;
            public IPAddress IPv4Mask => UnicastIPAddressInfo.IPv4Mask;

            private UdpClient _client = null;
            private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
            private bool _clientAlive = false;

            private bool enabled = true;
            public bool Enabled
            {
                get { return enabled; }
                set
                {
                    if (enabled == value)
                        return;
                    enabled = value;

                    Logger.LogDebug($"Client ({LocalIpAddress ?? BroadcastIpAddress}) {(enabled ? "Enabled" : "Disabled")}");
                }
            }
            public bool IsDisposing { get; private set; }
            public bool IsDisposed { get; private set; }


            public event EventHandler<Tuple<IPv4Address, UdpReceiveResult>> ReceivedData;

            internal NetworkClientBag(IPAddress broadcastIpAddress, UnicastIPAddressInformation unicastIPAddressInformation)
            {
                UnicastIPAddressInfo = unicastIPAddressInformation;
                BroadcastIpAddress = broadcastIpAddress;
                broadcastEndpoint = new IPEndPoint(broadcastIpAddress, Constants.ARTNET_PORT);
                Logger?.LogTrace($"Create Client ({LocalIpAddress})");
                _ = openClient();
            }

            private async Task closeClient()
            {
                await semaphoreSlim.WaitAsync();
                try
                {
                    _clientAlive = false;
                    _client?.Close();
                    (_client as IDisposable)?.Dispose();
                    notMatchingIpAdddresses.Clear();
                    matchingIpAdddresses.Clear();
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
                    Socket testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    testSocket.EnableBroadcast = true;
                    await testSocket.ConnectAsync(BroadcastIpAddress, Constants.ARTNET_PORT);
                    Logger?.LogTrace($"Client ({LocalIpAddress}): Socket is active");

                    Logger?.LogTrace($"Client ({LocalIpAddress}): initialize");
                    _client = new UdpClient();
                    _client.ExclusiveAddressUse = false;
                    _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    _client.EnableBroadcast = true;
                    var endpointIp = Tools.IsLinux() ? IPAddress.Any : LocalIpAddress;
                    IPEndPoint localEp = new IPEndPoint(IPAddress.Any, Constants.ARTNET_PORT);
                    _client.Client.Bind(localEp);
                    _clientAlive = true;
                    _ = StartListening();
                    Logger?.LogTrace($"Client ({LocalIpAddress}): initialized!");
                }
                catch (Exception e) { Logger?.LogError(e, $"Client ({LocalIpAddress}): Error on initialize"); }
                finally { semaphoreSlim?.Release(); }
            }

            private async Task StartListening()
            {
                try
                {
                    while (true)
                    {
                        if (this.IsDisposed || this.IsDisposing)
                            return;

                        UdpReceiveResult received = await _client.ReceiveAsync();

                        if (!Tools.IsWindows())
                            if (!Tools.IsInSubnet(LocalIpAddress, UnicastIPAddressInfo.IPv4Mask, received.RemoteEndPoint.Address))
                            {
                                Logger?.LogTrace($"Drop Packet Local:{LocalIpAddress}, Mask: {UnicastIPAddressInfo.IPv4Mask}, Remote: {received.RemoteEndPoint.Address}");
                                return;
                            }
                    

                        if (Enabled)
                            ReceivedData?.InvokeFailSafe(this, new Tuple<IPv4Address, UdpReceiveResult>(LocalIpAddress, received));
                    }
                }
                catch (SocketException) { }
                catch (Exception e) { Logger.LogError(e); _ = openClient(); }
            }

            private readonly List<IPAddress> notMatchingIpAdddresses = new List<IPAddress>();
            private readonly List<IPAddress> matchingIpAdddresses = new List<IPAddress>();
            public async Task<bool> MatchIP(IPAddress ip)
            {
                if (ip == null)
                    return false;
                if (ip.ToString().Equals("0.0.0.0"))
                    return false;

                if (notMatchingIpAdddresses.Contains(ip))
                    return false;
                if (matchingIpAdddresses.Contains(ip))
                    return true;

                var _ip = await GetLocalIP(ip);
                if (IPAddress.Equals(LocalIpAddress, _ip))
                {
                    matchingIpAdddresses.Add(ip);
                    return true;
                }

                notMatchingIpAdddresses.Add(ip);
                return false;
            }
            public async Task<IPAddress> GetLocalIP(IPAddress ip)
            {
                try
                {
                    Socket testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    testSocket.EnableBroadcast = true;
                    await testSocket.ConnectAsync(ip, Constants.ARTNET_PORT);
                    IPAddress localIpAddress = ((IPEndPoint)testSocket.LocalEndPoint).Address;
                    return localIpAddress;
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
                    if (!_clientAlive || _client?.Client == null)
                        return;

                    await _client.SendAsync(data, data.Length, new IPEndPoint(destinationIp, Constants.ARTNET_PORT));
//#if DEBUG
//                    Logger.LogTrace($"Send Packet to {destinationIp} -> {packet}");
//#endif
                    return;
                }
                catch (Exception e)
                {
                    Logger?.LogWarning($"Could not send packet: {e}");
                }
                finally
                {
                    semaphoreSlim.Release();
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
                    if (!_clientAlive || _client?.Client == null)
                        return;
                    await _client.SendAsync(data, data.Length, broadcastEndpoint);
//#if DEBUG
//                    Logger.LogTrace($"Send Packet to {broadcastEndpoint.Address} -> {packet}");
//#endif
                    return;
                }
                catch (Exception e)
                {
                    Logger?.LogWarning($"Could not send packet: {e}");
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

        internal ArtNet([CallerFilePath] string caller="", [CallerLineNumber] int line=-1)
        {
            if (Logger == null)
            {
                ApplicationLogging.LoggerFactory.AddProvider(new FileProvider());
                Logger = ApplicationLogging.CreateLogger<ArtNet>();
            }

            Logger?.LogTrace($"Initialize {caller} (Line: {line})");
            _updateNetworkClientsTimer = new System.Timers.Timer();
            _updateNetworkClientsTimer.Interval = 1000;
            _updateNetworkClientsTimer.Enabled = true;
            _updateNetworkClientsTimer.Elapsed += UpdateNetworkClientsTimer_Elapsed;
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
                DataReceived.InvokeFailSafe(this, EventArgs.Empty);
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
            ApplicationLogging.LoggerFactory.AddProvider(loggerProvider);
            loggerProviders.Add(loggerProvider);
        }

        private void UpdateNetworkClientsTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            updateNetworkClients();
        }

        private void ReceivedData(object sender, Tuple<IPv4Address, UdpReceiveResult> e)
        {
            ProcessReceivedData(e.Item2);
        }
        private void ProcessReceivedData(UdpReceiveResult result)
        {
            if (IsDisposed || IsDisposing)
                return;

            IPEndPoint RemoteIpEndPoint = result.RemoteEndPoint;
            byte[] received = result.Buffer;
            try
            {
                IPv4Address sourceIp = RemoteIpEndPoint.Address;
                if (Tools.TryDeserializePacket(received, out var packet))
                {
                    var nic = networkClients.Values.FirstOrDefault(n => Tools.IsInSubnet(n.LocalIpAddress,n.IPv4Mask, sourceIp));
                    if (nic != null)
                    {
                        //Logger?.LogTrace($"Process Network Packet:{packet} {Environment.NewLine} Local:{nic.LocalIpAddress}, Mask: {nic.IPv4Mask}, Remote: {sourceIp}");
                        processPacket(packet, nic.LocalIpAddress, sourceIp);
                    }
                    return;
                }
                Logger.LogWarning($"Can't deserialize Data to ArtNet-Packet");
            }
            catch (ObjectDisposedException ed) { Logger.LogTrace(ed); }
            catch (SocketException se) { Logger.LogTrace(se); }
            catch (Exception e) { Logger.LogError(e); }
        }
        private void processPacket(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp)
        {
#if DEBUG
            Logger.LogTrace($"Received Packet from {sourceIp} -> {packet}");
#endif
            foreach (var inst in instances) try
                {
                    ((IInstance)inst.Value).PacketReceived(packet, localIp, sourceIp);
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
        }

        public static bool IsNetworkAvailable(long? minimumSpeed=null)
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
                if (ipTomacAddressCache.ContainsKey(ip))
                    return ipTomacAddressCache[ip];
                MACAddress mac;
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

                IPAddress _ipToTest = ip;

                var nicWithThisIP = nics.FirstOrDefault(nic => nic.GetIPProperties().UnicastAddresses.Any(_ip => IPAddress.Equals(_ip.Address, _ipToTest)));
                if (nicWithThisIP != null)
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
        private async void updateNetworkClients()
        {
            if (LoopNetwork != null)
                return;

            if (IsDisposed || IsDisposing || updateNetworkSenaphoreSlim == null)
                return;

            if (updateNetworkSenaphoreSlim.CurrentCount == 0)
                return;

            await updateNetworkSenaphoreSlim?.WaitAsync();
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface @interface in interfaces)
                {
                    if (@interface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (@interface.OperationalStatus != OperationalStatus.Up) continue;
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

                        var ncb = new NetworkClientBag(new IPAddress(bytes), ipInfo);
                        networkClients.TryAdd((uint)_random.Next(), ncb);
                        Logger.LogDebug($"Added NetworkClient {ncb.LocalIpAddress}");
                        ncb.ReceivedData += ReceivedData;
                    }
                }
            }
            catch(Exception e)
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
                if (this.instances.Any(i => i.Value == instance))
                    return;
                if (this.instances.TryAdd((uint)new Random().Next(), instance))
                {
                    Logger?.LogDebug($"Added instance {instance.GetType().Name}: {instance.Name} ({instance.ShortName})");
                    OnInstanceAdded?.InvokeFailSafe(this, instance);
                }
            }
        }
        public void RemoveInstance(params AbstractInstance[] instances)
        {
            foreach (var instance in instances)
            {
                var toRemove = this.instances.FirstOrDefault(i => i.Value == instance);
                if (toRemove.Value == null)
                    return;
                if (this.instances.TryRemove(toRemove.Key, out _))
                {
                    Logger?.LogDebug($"Removed instance {instance.Name} ({instance.ShortName})");
                    OnInstanceRemoved?.InvokeFailSafe(this, instance);
                }
            }
        }

        internal async Task TrySendPacket(AbstractArtPacketCore packet, IPv4Address destinationIp)
        {
            if (this.IsDisposed || this.IsDisposing)
                return;
            if (LoopNetwork==null)
            {
                List<Task> tasks = new List<Task>();
                foreach (var ncb in networkClients.Values)
                    tasks.Add(Task.Run(async () => await ncb.TrySendPacket(packet, destinationIp)));
                await Task.WhenAll(tasks);
            }
            else
            {
                LoopNetwork.Send(packet);
            }
        }
        internal async Task TrySendBroadcastPacket(AbstractArtPacketCore packet)
        {
            if (this.IsDisposed || this.IsDisposing)
                return;

            if (LoopNetwork==null)
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
            foreach (var instance in instances)
                ((IDisposable)instance.Value).Dispose();
            instances.Clear();
            foreach (var net in networkClients)
            {
                net.Value.Enabled = false;
                net.Value.Dispose();
            }
            networkClients?.Clear();
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
}