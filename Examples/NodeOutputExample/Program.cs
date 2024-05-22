using ArtNetSharp;
using ArtNetSharp.Communication;

Console.WriteLine("Node Output Exampler!");

//Add Logging
//ArtNet.SetLoggerFectory(YOUR_LOGGER_FACTORY);

//Set Networkinterfaces
//var broadcastIp = new IPAddress(new byte[] { 2, 255, 255, 255 });
//ArtNet.Instance.NetworkClients.ToList().ForEach(ncb => ncb.Enabled = IPAddress.Equals(broadcastIp, ncb.BroadcastIpAddress));

// Create Instance
NodeInstance nodeInstance = new NodeInstance(ArtNet.Instance);
nodeInstance.Name = nodeInstance.ShortName = "Node Output Example";

// Configure Output Ports
for (byte i = 1; i <= 32; i++)
    nodeInstance.AddPortConfig(new PortConfig(i, new PortAddress((ushort)(i - 1)), true, false) { PortNumber = (byte)i, Type = EPortType.OutputFromArtNet, GoodOutput = new GoodOutput(outputStyle: GoodOutput.EOutputStyle.Continuous, isBeingOutputAsDMX: true)});

// Listen for new Data
nodeInstance.DMXReceived += (sender, e) =>
{
    if (!(sender is NodeInstance ni))
        return;

    // Can be called from anywere anytime without listen to the Event!!!
    var data = ni.GetReceivedDMX(e);

    Console.WriteLine($"Received date for {e}: {data.Length} bytes");
};

// Add Instance
ArtNet.Instance.AddInstance(nodeInstance);

Console.WriteLine("Press any key to Exit!");
Console.ReadLine();