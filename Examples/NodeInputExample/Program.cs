using ArtNetSharp;
using ArtNetSharp.Communication;
using System.Net;

Console.WriteLine("Node Input Example!");

//Add Logging
//ArtNet.SetLoggerFectory(YOUR_LOGGER_FACTORY);

//Set Networkinterfaces
var broadcastIp = new IPAddress(new byte[] { 2, 255, 255, 255 });
ArtNet.Instance.NetworkClients.ToList().ForEach(ncb => ncb.Enabled = IPAddress.Equals(broadcastIp, ncb.BroadcastIpAddress));

// Create Instance
NodeInstance nodeInstance = new NodeInstance();
nodeInstance.Name = nodeInstance.ShortName = "Node Input Example";

// Configure Input Ports
for (byte i = 1; i <= 4; i++)
    nodeInstance.AddPortConfig(new PortConfig(i, new PortAddress(i), false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet });

for (byte i = 11; i <= 14; i++)
    nodeInstance.AddPortConfig(new PortConfig(i, new PortAddress(i), false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet });

// Add Instance
ArtNet.Instance.AddInstance(nodeInstance);

// Genrerate some DMX-Data
byte[] data = new byte[512];
while (true)
{
    await Task.Delay(200);
    for (short k = 0; k < 512; k++)
        data[k]++;

    foreach (var port in nodeInstance.PortConfigs)
        nodeInstance.WriteDMXValues(port.PortAddress, data);
}