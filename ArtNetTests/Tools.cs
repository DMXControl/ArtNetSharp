using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

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

        internal static int ParseDotNetMajorVersion()
        {
            // Beispiel: ".NET 8.0.0"
            var parts = RuntimeInformation.FrameworkDescription.Split(' ');
            if (parts.Length >= 2 && Version.TryParse(parts[1], out var version))
            {
                return version.Major;
            }

            return -1; // oder Fehler werfen
        }
    }
}
