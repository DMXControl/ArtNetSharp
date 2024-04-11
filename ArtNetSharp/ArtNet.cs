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
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace ArtNetSharp
{
    public class ArtNet
    {
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

        private Dictionary<IPv4Address, MACAddress> ipTomacAddressCache = new Dictionary<IPv4Address, MACAddress>();

        private ConcurrentDictionary<uint, AbstractInstance> instances = new ConcurrentDictionary<uint, AbstractInstance>();
        public ReadOnlyCollection<AbstractInstance> Instances { get => instances.Values.ToList().AsReadOnly(); }

        private List<NetworkClientBag> networkClients = new List<NetworkClientBag>();
        public IReadOnlyCollection<NetworkClientBag> NetworkClients => networkClients.AsReadOnly();

        private System.Timers.Timer _updateNetworkClientsTimer = null;

        public class NetworkClientBag
        {
            private static Random _random = new Random();

            private readonly IPEndPoint broadcastEndpoint;
            public readonly IPAddress BroadcastIpAddress;
            public readonly UnicastIPAddressInformation UnicastIPAddressInfo;
            public IPAddress LocalIpAddress => UnicastIPAddressInfo.Address;

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

            public event EventHandler<Tuple<IPv4Address, UdpReceiveResult>> ReceivedData;

            internal NetworkClientBag(IPAddress broadcastIpAddress, UnicastIPAddressInformation unicastIPAddressInformation)
            {
                UnicastIPAddressInfo = unicastIPAddressInformation;
                BroadcastIpAddress = broadcastIpAddress;
                broadcastEndpoint = new IPEndPoint(broadcastIpAddress, Constants.ARTNET_PORT);
                _ = openClient();
            }

            private async Task openClient()
            {
                if (_client?.Client != null)
                    return;

                await semaphoreSlim.WaitAsync();
                try
                {
                    _clientAlive = false;
                    _client?.Close();
                    (_client as IDisposable)?.Dispose();
                    notMatchingIpAdddresses.Clear();
                    matchingIpAdddresses.Clear();
                    Logger?.LogTrace($"Client ({LocalIpAddress ?? BroadcastIpAddress}) Cleared");
                }
                catch (Exception e) { Logger.LogDebug(e); }
                finally { semaphoreSlim?.Release(); }

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

                        //if (!IsInSubnet(LocalIpAddress, UnicastIPAddressInfo.IPv4Mask, received.RemoteEndPoint.Address))
                        //{
                        //    Logger?.LogTrace($"Drop Packet Local:{LocalIpAddress}, Mask: {UnicastIPAddressInfo.IPv4Mask}, Remote: {received.RemoteEndPoint.Address}");
                        //    return;
                        //}
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
                if (!Enabled)
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
                if (!Enabled)
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
        }

        private ArtNet()
        {
            ApplicationLogging.LoggerFactory = Tools.LoggerFactory;

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
            _updateNetworkClientsTimer.Enabled = false;
            _updateNetworkClientsTimer.Elapsed -= _updateNetworkClientsTimer_Elapsed;
            networkClients.Clear();
        }

        private static List<ILoggerProvider> loggerProviders = new List<ILoggerProvider>();  
        public static void AddLoggProvider(ILoggerProvider loggerProvider)
        {
            return;
            if (loggerProviders.Contains(loggerProvider))
                return;
            Tools.LoggerFactory.AddProvider(loggerProvider);
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
            IPEndPoint RemoteIpEndPoint = result.RemoteEndPoint;
            byte[] received = result.Buffer;
            try
            {
                IPv4Address sourceIp = RemoteIpEndPoint.Address;
                processPacket(Tools.DeserializePacket(received), localIpAddress, sourceIp);
            }
            catch (ObjectDisposedException ed) { Logger.LogTrace(ed); }
            catch (SocketException se) { Logger.LogTrace(se); }
            catch (Exception e) { Logger.LogError(e); }
        }
        private void processPacket(AbstractArtPacketCore packet, IPv4Address localIp, IPv4Address sourceIp)
        {
            if (packet == null)
            {
                Logger?.LogWarning($"Received Non-Art-Net packet from {sourceIp}, discarding");
                return;
            }
//#if DEBUG
            //if (IPv4Address.Equals(localIp, sourceIp))
            //    return;

            Logger.LogTrace($"Received Packet from {sourceIp} -> {packet}");
//#endif
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
        private void updateNetworkClients()
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            var tmp = new List<IPAddress>(interfaces.Length); //At least 1 IP per Interface
            foreach (NetworkInterface @interface in interfaces)
            {
                if (@interface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (@interface.OperationalStatus != OperationalStatus.Up) continue;
                UnicastIPAddressInformationCollection unicastIpInfoCol = @interface.GetIPProperties().UnicastAddresses;
                foreach (UnicastIPAddressInformation ipInfo in unicastIpInfoCol)
                {
                    if (networkClients.Any(nc => nc.UnicastIPAddressInfo.Equals(ipInfo)))
                        continue;

                    uint ipAddress = BitConverter.ToUInt32(ipInfo.Address.GetAddressBytes(), 0);
                    uint ipMaskV4 = BitConverter.ToUInt32(ipInfo.IPv4Mask.GetAddressBytes(), 0);
                    uint broadCastIpAddress = ipAddress | ~ipMaskV4;

                    byte[] bytes = BitConverter.GetBytes(broadCastIpAddress);
                    if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255) // Limited Broadcast: Art-Net packets should not be broadcast to the Limited Broadcast address of 255.255.255.255.
                        continue;// 1.4dh 19/7/2023 - 10 -

                    var ncb = new NetworkClientBag(new IPAddress(bytes), ipInfo);
                    networkClients.Add(ncb);
                    Logger.LogDebug($"Added NetworkClient {ncb.LocalIpAddress}");
                    ncb.ReceivedData += ReceivedData;
                }
            }
        }

        public void AddInstance(AbstractInstance instance)
        {
            if (this.instances.Any(i => i.Value == instance))
                return;
            this.instances.TryAdd((uint)new Random().Next(), instance);
            Logger?.LogDebug($"Added instance {instance.GetType().Name}: {instance.Name} ({instance.ShortName})");
        }
        public void RemoveInstance(AbstractInstance instance)
        {
            var toRemove = this.instances.FirstOrDefault(i => i.Value == instance);
            if (toRemove.Value == null)
                return;
            this.instances.TryRemove(toRemove.Key, out _);
            Logger?.LogDebug($"Removed instance {instance.ShortName}");
        }

        internal async Task TrySendPacket(AbstractArtPacketCore packet, IPv4Address destinationIp)
        {
            List<Task> tasks = new List<Task>();
            foreach (var ncb in networkClients)
                tasks.Add(Task.Run(async () => await ncb.TrySendPacket(packet, destinationIp)));
            await Task.WhenAll(tasks);
        }
        internal async Task TrySendBroadcastPacket(AbstractArtPacketCore packet)
        {
            List<Task> tasks = new List<Task>();
            foreach (var ncb in networkClients)
                tasks.Add(Task.Run(async () => await ncb.TrySendBroadcastPacket(packet)));
            await Task.WhenAll(tasks);
        }
    }
}