using RDMSharp;
using System.Net.NetworkInformation;

namespace ArtNetTests
{
    internal static class Tools
    {
        internal static Tuple<IPv4Address, IPv4Address>[] GetIpAddresses()
        {
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            List<Tuple<IPv4Address, IPv4Address>> list = new List<Tuple<IPv4Address, IPv4Address>>();
            // Iterate through each network interface
            foreach (NetworkInterface networkInterface in networkInterfaces)
            {
                // Filter out loopback and non-operational interfaces
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                // Get IP properties for the network interface
                IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();

                // Iterate through each unicast IP address assigned to the interface
                foreach (UnicastIPAddressInformation ipInfo in ipProperties.UnicastAddresses)
                    if (ipInfo.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) // IPv4 addresses only
                        list.Add(new Tuple<IPv4Address, IPv4Address>(ipInfo.Address, ipInfo.IPv4Mask));
            }

            return list.ToArray();
        }
    }
}
