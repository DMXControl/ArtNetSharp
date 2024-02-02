using ArtNetSharp;
using ArtNetSharp.Communication;

Console.WriteLine("Node Input Example!");

// Create Instance
NodeInstance nodeInstance = new NodeInstance();
nodeInstance.Name = nodeInstance.ShortName = "Node Input Example";

// Configure Input Ports
for (ushort i = 0; i < 4; i++)
    nodeInstance.AddPortConfig(new PortConfig(i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet });

for (ushort i = 10; i < 14; i++)
    nodeInstance.AddPortConfig(new PortConfig(i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet });

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