using ArtNetSharp;
using ArtNetSharp.Communication;

Console.WriteLine("Controller Example!");

//Add Logging
//ArtNet.SetLoggerFectory(YOUR_LOGGER_FACTORY);

//Set Networkinterfaces
//var broadcastIp = new IPAddress(new byte[] { 2, 255, 255, 255 });
//ArtNet.Instance.NetworkClients.ToList().ForEach(ncb => ncb.Enabled = IPAddress.Equals(broadcastIp, ncb.BroadcastIpAddress));

// Create Instance
ControllerInstance controllerInstance = new ControllerInstance(ArtNet.Instance);
controllerInstance.Name = controllerInstance.ShortName = "Controller Example";

// Configure Ports
for (byte i = 1; i <= 32; i++)
    controllerInstance.AddPortConfig(new PortConfig(i, new PortAddress((ushort)(i - 1)), false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet });

// Add Instance
ArtNet.Instance.AddInstance(controllerInstance);

// Genrerate some DMX-Data
byte[] data = new byte[512];
while (true)
{
    await Task.Delay(200);
    for (short k = 0; k < 512; k++)
        data[k]++;

    for (ushort i = 0; i < 32; i++)
        controllerInstance.WriteDMXValues(i, data);
}