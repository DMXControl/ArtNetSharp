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
                if (instance == null)
                    instance = new ArtNet();
                return instance;
            }
        }

        public event EventHandler<AbstractInstance> OnInstanceAdded;
        public event EventHandler<AbstractInstance> OnInstanceRemoved;

        private Dictionary<IPv4Address, MACAddress> ipTomacAddressCache = new Dictionary<IPv4Address, MACAddress>();

        private ConcurrentDictionary<uint, AbstractInstance> instances = new ConcurrentDictionary<uint, AbstractInstance>();
        public ReadOnlyCollection<AbstractInstance> Instances { get => instances.Values.ToList().AsReadOnly(); }

        private ConcurrentDictionary<uint,NetworkClientBag> networkClients = new ConcurrentDictionary<uint, NetworkClientBag>();
        public IReadOnlyCollection<NetworkClientBag> NetworkClients => networkClients.Values.ToList().AsReadOnly();

        private System.Timers.Timer _updateNetworkClientsTimer = null;

        public bool IsDisposing { get; private set; }
        public bool IsDisposed { get; private set; }

        public class NetworkClientBag: IDisposable
        {
            private static ILogger<NetworkClientBag> Logger = ApplicationLogging.CreateLogger<NetworkClientBag>();
            private readonly IPEndPoint broadcastEndpoint;
            public readonly IPAddress BroadcastIpAddress;
            public readonly UnicastIPAddressInformation UnicastIPAddressInfo;
            public IPAddress LocalIpAddress => UnicastIPAddressInfo.Address;
            public IPAddress IPv4Mask => UnicastIPAddressInfo.IPv4Mask;

            private UdpClient _client = null;
            private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
            private bool _clientAlive = false;

            public bool enabled = true;
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
                        UdpReceiveResult received = await _client.ReceiveAsync();


                        //if (Tools.IsLinux())
                            if (!IsInSubnet(LocalIpAddress, UnicastIPAddressInfo.IPv4Mask, received.RemoteEndPoint.Address))
                            {
                                Logger?.LogTrace($"Drop Packet Local:{LocalIpAddress}, Mask: {UnicastIPAddressInfo.IPv4Mask}, Remote: {received.RemoteEndPoint.Address}");
                                return;
                            }

                        Logger?.LogTrace($"Allowed Packet Local:{LocalIpAddress}, Mask: {UnicastIPAddressInfo.IPv4Mask}, Remote: {received.RemoteEndPoint.Address}");
                        if (Enabled)
                            ReceivedData?.Invoke(this, new Tuple<IPv4Address, UdpReceiveResult>(LocalIpAddress, received));
                    }
                }
                catch (Exception e) { Logger.LogError(e); _ = openClient(); }
            }
            public static bool IsInSubnet(IPAddress ip, IPAddress mask, IPAddress target)
            {
                try
                {
                    // Get bytes of IP address and subnet mask
                    byte[] ipBytes = ip.GetAddressBytes();
                    byte[] maskBytes = mask.GetAddressBytes();
                    byte[] targetBytes = target.GetAddressBytes();

                    // Perform bitwise AND operation between IP address and subnet mask
                    for (int i = 0; i < ipBytes.Length; i++)
                    {
                        if ((ipBytes[i] & maskBytes[i]) != (targetBytes[i] & maskBytes[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    // Handle any parsing errors
                    Logger?.LogError(ex);
                    return false;
                }
            }

            private List<IPAddress> notMatchingIpAdddresses = new List<IPAddress>();
            private List<IPAddress> matchingIpAdddresses = new List<IPAddress>();
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
#if DEBUG
                    Logger.LogTrace($"Send Packet to {destinationIp} -> {packet}");
#endif
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
#if DEBUG
                    Logger.LogTrace($"Send Packet to {broadcastEndpoint.Address} -> {packet}");
#endif
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

            public void Dispose()
            {
                if (IsDisposed || IsDisposing)
                    return;
                IsDisposing = true;
                _ = closeClient();
                IsDisposed = true;
                IsDisposing = false;
            }
        }

        private ArtNet()
        {
            ApplicationLogging.LoggerFactory.AddProvider(new FileProvider());

            Logger = ApplicationLogging.CreateLogger<ArtNet>();
            Logger.LogTrace("Initialized!");

            _updateNetworkClientsTimer = new System.Timers.Timer();
            _updateNetworkClientsTimer.Interval = 1000;
            _updateNetworkClientsTimer.Enabled = true;
            _updateNetworkClientsTimer.Elapsed += _updateNetworkClientsTimer_Elapsed;
            updateNetworkClients();
        }
        ~ArtNet()
        {
            ((IDisposable)this).Dispose();
        }

        internal static void Clear()
        {
            if (instance == null)
                return;

            var old = instance;
            instance= new ArtNet();

            old.RemoveInstance(old.Instances.ToArray());
            ((IDisposable)old).Dispose();
        }

        private static List<ILoggerProvider> loggerProviders = new List<ILoggerProvider>();  
        public static void AddLoggProvider(ILoggerProvider loggerProvider)
        {
            if (loggerProviders.Contains(loggerProvider))
                return;
            ApplicationLogging.LoggerFactory.AddProvider(loggerProvider);
            loggerProviders.Add(loggerProvider);
        }

        private void _updateNetworkClientsTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            updateNetworkClients();
        }

        private void ReceivedData(object sender, Tuple<IPv4Address, UdpReceiveResult> e)
        {
            processReceivedData(e.Item1, e.Item2);
        }
        private void processReceivedData(IPv4Address localIpAddress, UdpReceiveResult result)
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
                    processPacket(packet, localIpAddress, sourceIp);
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
            instances.Values.ToList().ForEach(_instance => { ((IInstance)_instance).PacketReceived(packet, localIp, sourceIp); });
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
        private SemaphoreSlim updateNetworkSenaphoreSlim = new SemaphoreSlim(1);
        private async void updateNetworkClients()
        {
            if (IsDisposed || IsDisposing)
                return;

            if (updateNetworkSenaphoreSlim.CurrentCount == 0)
                return;

            await updateNetworkSenaphoreSlim.WaitAsync();
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
                updateNetworkSenaphoreSlim.Release();
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
                    OnInstanceAdded(this, instance);
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
                    OnInstanceRemoved(this, instance);
                }
            }
        }

        internal async Task TrySendPacket(AbstractArtPacketCore packet, IPv4Address destinationIp)
        {
            List<Task> tasks = new List<Task>();
            foreach (var ncb in networkClients.Values)
                tasks.Add(Task.Run(async () => await ncb.TrySendPacket(packet, destinationIp)));
            await Task.WhenAll(tasks);
        }
        internal async Task TrySendBroadcastPacket(AbstractArtPacketCore packet)
        {
            List<Task> tasks = new List<Task>();
            foreach (var ncb in networkClients.Values)
                tasks.Add(Task.Run(async () => await ncb.TrySendBroadcastPacket(packet)));
            await Task.WhenAll(tasks);
        }

        void IDisposable.Dispose()
        {
            if (IsDisposed || IsDisposing)
                return;
            IsDisposing = true;

            _updateNetworkClientsTimer.Enabled = false;
            _updateNetworkClientsTimer.Elapsed -= _updateNetworkClientsTimer_Elapsed;
            _updateNetworkClientsTimer = null;
            networkClients.Clear();
            loggerProviders.Clear();
            foreach (var instance in instances)
                ((IDisposable)instance.Value).Dispose();
            instances.Clear();
            foreach (var net in networkClients)
            {
                net.Value.Enabled = false;
                net.Value.Dispose();
            }
            networkClients.Clear();

            updateNetworkSenaphoreSlim.Release();
            updateNetworkSenaphoreSlim.Dispose();

            ipTomacAddressCache.Clear();
            OnInstanceAdded = null;
            OnInstanceRemoved = null;
            Logger = null;
            IsDisposed = true;
            IsDisposing = false;

            GC.SuppressFinalize(this);
        }
    }
}